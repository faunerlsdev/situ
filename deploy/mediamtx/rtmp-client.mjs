// Minimal RTMP client for the HaishinKit 2.2.5 and RootEncoder publish sequences.
// Chunk headers follow HaishinKit: type 0, message stream id little-endian.

import { randomBytes } from 'node:crypto';
import { connect as connectTcp } from 'node:net';
import { connect as connectTls } from 'node:tls';

const TYPE_CHUNK_SIZE = 0x01;
const TYPE_COMMAND = 0x14;
const CONTROL_CSID = 2;
const COMMAND_CSID = 3;

export function amfString(value) {
  const body = Buffer.from(value, 'utf8');
  const out = Buffer.alloc(3 + body.length);
  out[0] = 0x02;
  out.writeUInt16BE(body.length, 1);
  out.set(body, 3);
  return out;
}

export function amfNumber(value) {
  const out = Buffer.alloc(9);
  out[0] = 0x00;
  out.writeDoubleBE(value, 1);
  return out;
}

export function amfBoolean(value) {
  return Buffer.from([0x01, value ? 1 : 0]);
}

export function amfNull() {
  return Buffer.from([0x05]);
}

export function amfObject(entries) {
  const parts = [Buffer.from([0x03])];
  for (const [key, value] of entries) {
    const name = Buffer.from(key, 'utf8');
    const header = Buffer.alloc(2 + name.length);
    header.writeUInt16BE(name.length, 0);
    header.set(name, 2);
    parts.push(header, value);
  }
  parts.push(Buffer.from([0x00, 0x00, 0x09]));
  return Buffer.concat(parts);
}

function command(name, transactionId, object, args) {
  return Buffer.concat([
    amfString(name),
    amfNumber(transactionId),
    object,
    ...args,
  ]);
}

// One RTMP message, split the way HaishinKit splits after it raises chunkSizeS.
export function encodeMessage(csid, type, streamId, body, chunkSize) {
  const chunks = [];
  let offset = 0;
  const write = (fmt, payload) => {
    const basic = Buffer.from([(fmt << 6) | (csid & 0x3f)]);
    chunks.push(Buffer.concat([basic, payload]));
  };
  do {
    const take = Math.min(chunkSize, body.length - offset);
    if (offset === 0) {
      const header = Buffer.alloc(11);
      header.writeUIntBE(0, 0, 3);
      header.writeUIntBE(body.length, 3, 3);
      header[6] = type;
      header.writeUInt32LE(streamId, 7);
      write(0, Buffer.concat([header, body.subarray(0, take)]));
    } else {
      write(3, body.subarray(offset, offset + take));
    }
    offset += take;
  } while (offset < body.length);
  if (body.length === 0) {
    const header = Buffer.alloc(11);
    header[6] = type;
    header.writeUInt32LE(streamId, 7);
    write(0, header);
  }
  return Buffer.concat(chunks);
}

function firstAmfString(body) {
  if (body.length < 3 || body[0] !== 0x02) {
    return '';
  }
  const length = body.readUInt16BE(1);
  return body.subarray(3, 3 + length).toString('utf8');
}

class ChunkReader {
  constructor() {
    this.buffer = Buffer.alloc(0);
    this.chunkSize = 128;
    this.streams = new Map();
  }

  push(chunk) {
    this.buffer = Buffer.concat([this.buffer, chunk]);
  }

  // Returns one reassembled message, or null when more bytes are required.
  read() {
    while (this.buffer.length > 0) {
      const basic = this.buffer[0];
      const fmt = basic >> 6;
      const csid = basic & 0x3f;
      if (csid < 2) {
        throw new Error(`extended chunk stream id ${csid}`);
      }
      const headerLength = [11, 7, 3, 0][fmt];
      if (this.buffer.length < 1 + headerLength) {
        return null;
      }
      const header = this.buffer.subarray(1, 1 + headerLength);
      const timestamp = headerLength >= 3 ? header.readUIntBE(0, 3) : 0;
      const extended = headerLength >= 3 && timestamp === 0xffffff;
      if (this.buffer.length < 1 + headerLength + (extended ? 4 : 0)) {
        return null;
      }
      const previous = this.streams.get(csid) ?? {
        bodyLength: 0,
        type: 0,
        received: 0,
        parts: [],
      };
      let bodyLength = previous.bodyLength;
      let type = previous.type;
      if (fmt === 0 || fmt === 1) {
        bodyLength = header.readUIntBE(3, 3);
        type = header[6];
      }
      const received = fmt === 3 ? previous.received : 0;
      const chunkBody = Math.min(bodyLength - received, this.chunkSize);
      const start = 1 + headerLength + (extended ? 4 : 0);
      if (this.buffer.length < start + chunkBody) {
        return null;
      }
      const piece = this.buffer.subarray(start, start + chunkBody);
      this.buffer = this.buffer.subarray(start + chunkBody);
      const parts = fmt === 3 ? previous.parts : [];
      parts.push(Buffer.from(piece));
      const got = received + chunkBody;
      if (got < bodyLength) {
        this.streams.set(csid, { bodyLength, type, received: got, parts });
        continue;
      }
      this.streams.set(csid, { bodyLength, type, received: 0, parts: [] });
      const body = Buffer.concat(parts);
      if (type === TYPE_CHUNK_SIZE && body.length === 4) {
        this.chunkSize = body.readUInt32BE(0);
      }
      return { type, body, csid };
    }
    return null;
  }
}

function connectObject(app, tcUrl) {
  return amfObject([
    ['app', amfString(app)],
    ['flashVer', amfString('FMLE/3.0 (compatible; FMSc/1.0)')],
    ['tcUrl', amfString(tcUrl)],
    ['fpad', amfBoolean(false)],
    ['capabilities', amfNumber(239)],
    ['audioCodecs', amfNumber(1024)],
    ['videoCodecs', amfNumber(128)],
    ['videoFunction', amfNumber(1)],
    ['objectEncoding', amfNumber(0)],
  ]);
}

function openSocket(host, port, tls) {
  return new Promise((resolve, reject) => {
    const socket = tls
      ? connectTls({ host, port, rejectUnauthorized: false, servername: 'localhost' }, () => resolve(socket))
      : connectTcp({ host, port }, () => resolve(socket));
    socket.once('error', reject);
  });
}

function readExact(socket, reader, length) {
  return new Promise((resolve, reject) => {
    const take = () => {
      if (reader.buffer.length >= length) {
        const out = reader.buffer.subarray(0, length);
        reader.buffer = reader.buffer.subarray(length);
        cleanup();
        resolve(out);
      }
    };
    const onData = (chunk) => {
      reader.push(chunk);
      take();
    };
    const onError = (error) => {
      cleanup();
      reject(error);
    };
    const onClose = () => {
      cleanup();
      reject(new Error('socket closed during handshake'));
    };
    const cleanup = () => {
      socket.off('data', onData);
      socket.off('error', onError);
      socket.off('close', onClose);
    };
    socket.on('data', onData);
    socket.on('error', onError);
    socket.on('close', onClose);
    take();
  });
}

async function handshake(socket, reader) {
  const c1 = Buffer.alloc(1536);
  randomBytes(1528).copy(c1, 8);
  socket.write(Buffer.concat([Buffer.from([0x03]), c1]));
  const response = await readExact(socket, reader, 1 + 1536 + 1536);
  if (response[0] !== 0x03) {
    throw new Error(`unexpected S0 ${response[0]}`);
  }
  socket.write(response.subarray(1, 1 + 1536));
}

function send(socket, bytes) {
  return new Promise((resolve, reject) => {
    socket.write(bytes, (error) => (error ? reject(error) : resolve(bytes.length)));
  });
}

async function nextMessage(socket, reader, timeoutMs) {
  const pending = reader.read();
  if (pending) {
    return pending;
  }
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      cleanup();
      const error = new Error('client read timeout');
      error.code = 'CLIENT_TIMEOUT';
      reject(error);
    }, timeoutMs);
    const onData = (chunk) => {
      reader.push(chunk);
      const message = reader.read();
      if (message) {
        cleanup();
        resolve(message);
      }
    };
    const onError = (error) => {
      cleanup();
      reject(error);
    };
    const onClose = () => {
      cleanup();
      reject(new Error('socket closed'));
    };
    const cleanup = () => {
      clearTimeout(timer);
      socket.off('data', onData);
      socket.off('error', onError);
      socket.off('close', onClose);
    };
    socket.on('data', onData);
    socket.on('error', onError);
    socket.on('close', onClose);
  });
}

/**
 * @param {'haishin' | 'haishin-fragmented' | 'haishin-unfragmented' | 'haishin-fmle-wait' | 'root'} mode
 * haishin: Set Chunk Size 8192, then releaseStream / FCPublish / createStream / publish
 *   with the id and query in the stream name and an empty app. This is HaishinKit 2.2.5
 *   once createStream is sent without waiting for releaseStream and FCPublish.
 * haishin-fragmented: the same commands, chunked at 128, without raising the chunk size.
 * haishin-unfragmented: the same commands as one chunk larger than 128, without Set Chunk Size.
 * haishin-fmle-wait: Set Chunk Size, releaseStream and FCPublish, then hold the socket.
 *   MediaMTX does not reply to those two commands. HaishinKit 2.2.5 awaits them
 *   (async let inside the if) before createStream, so the server hits readTimeout.
 * root: RootEncoder shape. tcUrl carries the id and the query; the publish name is empty.
 */
export async function publishSequence({
  host = '127.0.0.1',
  port,
  mode,
  id,
  key,
  timeoutMs = 12_000,
  tls = false,
}) {
  const streamName = `${id}?user=situ&pass=${key}`;
  const socket = await openSocket(host, port, tls);
  const reader = new ChunkReader();
  const sent = [];
  const received = [];
  let failed = '';
  try {
    await handshake(socket, reader);
    const scheme = tls ? 'rtmps' : 'rtmp';
    const tcUrl = mode === 'root'
      ? `${scheme}://${host}:${port}/${streamName}`
      : `${scheme}://${host}:${port}`;
    const app = mode === 'root' ? streamName : '';
    const connectBody = command('connect', 1, connectObject(app, tcUrl), []);
    const connectBytes = encodeMessage(COMMAND_CSID, TYPE_COMMAND, 0, connectBody, 128);
    sent.push({ name: 'connect', payload: connectBody.length, wire: connectBytes.length });
    await send(socket, connectBytes);

    const deadline = Date.now() + timeoutMs;
    let connected = false;
    while (!connected && Date.now() < deadline) {
      const message = await nextMessage(socket, reader, deadline - Date.now());
      const name = message.type === TYPE_COMMAND ? firstAmfString(message.body) : '';
      received.push({ type: message.type, name, bytes: message.body.length });
      if (name === '_result' || name === '_error') {
        connected = true;
      }
    }
    if (!connected) {
      throw new Error('no connect result');
    }

    const outboundChunk = mode === 'haishin' || mode === 'haishin-fmle-wait' ? 8192 : 128;
    if (mode === 'haishin' || mode === 'haishin-fmle-wait') {
      const size = Buffer.alloc(4);
      size.writeUInt32BE(8192, 0);
      const bytes = encodeMessage(CONTROL_CSID, TYPE_CHUNK_SIZE, 0, size, 128);
      sent.push({ name: 'setChunkSize', payload: 4, wire: bytes.length });
      await send(socket, bytes);
    }

    let transaction = 2;
    const commands = [];
    if (mode === 'root') {
      commands.push(['createStream', amfNull()]);
      commands.push(['publish', amfNull(), amfString(''), amfString('live')]);
    } else {
      commands.push(['releaseStream', amfNull(), amfString(streamName)]);
      commands.push(['FCPublish', amfNull(), amfString(streamName)]);
      commands.push(['createStream', amfNull()]);
      commands.push(['publish', amfNull(), amfString(streamName), amfString('live')]);
    }
    let stalled = false;
    for (const [name, ...args] of commands) {
      const body = command(name, transaction, args[0], args.slice(1));
      transaction += 1;
      if (mode === 'haishin-fmle-wait' && name === 'createStream') {
        stalled = true;
        break;
      }
      const chunkSize = mode === 'haishin-unfragmented' ? Math.max(outboundChunk, body.length) : outboundChunk;
      const bytes = encodeMessage(COMMAND_CSID, TYPE_COMMAND, name === 'publish' ? 1 : 0, body, chunkSize);
      sent.push({ name, payload: body.length, wire: bytes.length, chunkSize });
      await send(socket, bytes);
      if (name !== 'createStream' && name !== 'publish') {
        continue;
      }
      const waitUntil = Date.now() + Math.min(name === 'createStream' ? timeoutMs : 4_000, Math.max(500, deadline - Date.now()));
      let answered = false;
      while (!answered && Date.now() < waitUntil) {
        try {
          const message = await nextMessage(socket, reader, Math.max(1, waitUntil - Date.now()));
          const reply = message.type === TYPE_COMMAND ? firstAmfString(message.body) : '';
          received.push({ type: message.type, name: reply, bytes: message.body.length, after: name });
          if (reply === '_result' || reply === '_error' || reply === 'onStatus') {
            answered = true;
          }
        } catch (error) {
          if (error.code === 'CLIENT_TIMEOUT') {
            received.push({ type: 0, name: 'timeout', bytes: 0, after: name });
            break;
          }
          throw error;
        }
      }
      // HaishinKit does not send publish until createStream's _result arrives.
      if (!answered && name === 'createStream') {
        stalled = true;
        break;
      }
    }
    if (stalled) {
      await new Promise((resolve) => {
        setTimeout(resolve, Math.max(0, deadline - Date.now()));
      });
    }
  } catch (error) {
    failed = error instanceof Error ? error.message : String(error);
  } finally {
    socket.destroy();
  }
  return { mode, sent, received, failed };
}
