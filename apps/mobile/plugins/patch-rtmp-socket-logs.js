const fs = require('fs');
const path = require('path');

const MARKER = '[situ-rtmps]';

/**
 * CocoaPods checks HaishinKit 2.2.5 out from the tag, so pod install wipes a
 * hand edit. This reapplies the RTMPSocket debug logs and the createStream
 * fix. The logs are DEBUG-only and include no host, URL, or certificate bytes.
 */
function patchRtmpSocket(source) {
  if (source.includes(MARKER)) {
    return source;
  }
  let next = source.replace('import Network\n', 'import Network\nimport Security\n');
  next = next.replace(
    `        switch securityLevel {
        case .ssLv2, .ssLv3, .tlSv1, .negotiatedSSL:
            parameters = .tls
        default:
            parameters = .tcp
        }
    }`,
    `        switch securityLevel {
        case .ssLv2, .ssLv3, .tlSv1, .negotiatedSSL:
            parameters = Self.makeTLSParameters()
        default:
            parameters = .tcp
        }
    }

    /// Reports the system trust result after the server certificate arrives.
    /// It does not send the ClientHello and does not accept an untrusted certificate.
    nonisolated private static func makeTLSParameters() -> NWParameters {
        let options = NWProtocolTLS.Options()
        let queue = DispatchQueue(label: "com.situ.rtmps.tls-verify")
        sec_protocol_options_set_verify_block(options.securityProtocolOptions, { _, trust, complete in
            let secTrust = sec_trust_copy_ref(trust).takeRetainedValue()
            var cfError: CFError?
            let ok = SecTrustEvaluateWithError(secTrust, &cfError)
            #if DEBUG
            let code: String
            if let cfError {
                let ns = cfError as Swift.Error as NSError
                code = "\\(ns.domain) \\(ns.code)"
            } else {
                code = ok ? "ok" : "rejected"
            }
            NSLog("[situ-rtmps] tls-trust ok=%d code=%@", ok ? 1 : 0, code)
            #endif
            complete(ok)
        }, queue)
        return NWParameters(tls: options, tcp: NWProtocolTCP.Options())
    }`,
  );
  next = next.replace(
    `        totalBytesIn = 0
        totalBytesOut = 0
        queueBytesOut = 0
        do {`,
    `        totalBytesIn = 0
        totalBytesOut = 0
        queueBytesOut = 0
        #if DEBUG
        NSLog("[situ-rtmps] connect port=%d tls=%d", port, securityLevel == .none ? 0 : 1)
        #endif
        do {`,
  );
  next = next.replace(
    `    func send(_ data: Data) {
        guard connected else {
            return
        }`,
    `    func send(_ data: Data) {
        #if DEBUG
        if totalBytesOut == 0 && queueBytesOut == 0 {
            NSLog("[situ-rtmps] first-send bytes=%d connected=%d", data.count, connected ? 1 : 0)
        }
        #endif
        guard connected else {
            return
        }`,
  );
  next = next.replace(
    `    private func stateDidChange(to state: NWConnection.State) {
        switch state {`,
    `    private func stateDidChange(to state: NWConnection.State) {
        #if DEBUG
        NSLog("[situ-rtmps] state=%@", Self.stateLabel(state))
        #endif
        switch state {`,
  );
  next = next.replace(
    `    private func viabilityDidChange(to viability: Bool) {
        logger.info("Connection viability changed to ", viability)`,
    `    private func viabilityDidChange(to viability: Bool) {
        #if DEBUG
        NSLog("[situ-rtmps] viability=%d", viability ? 1 : 0)
        #endif
        logger.info("Connection viability changed to ", viability)`,
  );
  next = next.replace(
    'extension RTMPSocket: NetworkTransportReporter {',
    `extension RTMPSocket {
    nonisolated fileprivate static func stateLabel(_ state: NWConnection.State) -> String {
        switch state {
        case .setup:
            return "setup"
        case .preparing:
            return "preparing"
        case .ready:
            return "ready"
        case .waiting(let error):
            return "waiting \\(Self.errorLabel(error))"
        case .failed(let error):
            return "failed \\(Self.errorLabel(error))"
        case .cancelled:
            return "cancelled"
        @unknown default:
            return "unknown"
        }
    }

    nonisolated fileprivate static func errorLabel(_ error: NWError) -> String {
        switch error {
        case .posix(let code):
            return "posix \\(code.rawValue)"
        case .dns(let code):
            return "dns \\(code)"
        case .tls(let code):
            return "tls \\(code)"
        default:
            return "other"
        }
    }
}

extension RTMPSocket: NetworkTransportReporter {`,
  );
  if (!next.includes(MARKER)) {
    throw new Error('[situ] No se pudo insertar el registro de RTMPS en RTMPSocket.swift');
  }
  return next;
}

const CREATE_STREAM_BLOCK = `        if let fcPublishName {
            // FMLE-compatible sequences
            async let _ = connection?.call("releaseStream", arguments: fcPublishName)
            async let _ = connection?.call("FCPublish", arguments: fcPublishName)
        }`;

const CREATE_STREAM_FIX = `        if let fcPublishName {
            // These commands share this scope. Swift waits for an async let at the
            // end of its scope, and MediaMTX never replies to releaseStream or
            // FCPublish, so createStream was not sent before the read timeout.
            let publishName = fcPublishName
            Task {
                _ = try? await connection?.call("releaseStream", arguments: publishName)
            }
            Task {
                _ = try? await connection?.call("FCPublish", arguments: publishName)
            }
        }`;

function patchCreateStream(source) {
  if (source.includes('MediaMTX never replies to releaseStream')) {
    return source;
  }
  if (!source.includes(CREATE_STREAM_BLOCK)) {
    throw new Error('[situ] No se encontró el async let de createStream en RTMPStream.swift');
  }
  return source.replace(CREATE_STREAM_BLOCK, CREATE_STREAM_FIX);
}

function patchFile(filePath) {
  const source = fs.readFileSync(filePath, 'utf8');
  const next = filePath.endsWith('RTMPStream.swift') ? patchCreateStream(source) : patchRtmpSocket(source);
  if (next !== source) {
    // CocoaPods checks the tag out read-only.
    fs.chmodSync(filePath, 0o644);
    fs.writeFileSync(filePath, next);
  }
  return next !== source;
}

function main() {
  const root = path.resolve(__dirname, '..');
  const socket = path.join(
    root,
    'ios/Pods/RTMPHaishinKit/RTMPHaishinKit/Sources/RTMP/RTMPSocket.swift',
  );
  const stream = path.join(
    root,
    'ios/Pods/RTMPHaishinKit/RTMPHaishinKit/Sources/RTMP/RTMPStream.swift',
  );
  if (fs.existsSync(socket)) {
    patchFile(socket);
  }
  if (fs.existsSync(stream)) {
    patchFile(stream);
  }
}

module.exports = { patchRtmpSocket, patchCreateStream, patchFile };

if (require.main === module) {
  main();
}
