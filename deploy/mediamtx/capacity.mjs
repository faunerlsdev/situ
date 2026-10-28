import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { availableParallelism } from 'node:os';
import { pathToFileURL } from 'node:url';

/**
 * Rough live capacity for one VPS that also runs the rest of SITU.
 *
 * Assumptions, on purpose conservative:
 * - 720p H.264 at 2.5 Mbps plus AAC at 128 kbps. MediaMTX remuxes; it does not transcode.
 * - One live path costs about 0.4 core and 80 MB. Each HLS viewer costs about 0.02 core and 12 MB.
 * - Keep 1 core and 25% of RAM for the API, PostgreSQL, nginx and the OS.
 * - A recording grows about 1.1 GB per hour per stream.
 * - The NIC is not measured. About 3 Mbps of egress per viewer can fill a small port before the CPU does.
 */

export const STREAM_MBPS = 2.5 + 0.128;
export const VIEWER_EGRESS_MBPS = Number((STREAM_MBPS * 1.15).toFixed(2));
export const RECORDING_GB_PER_HOUR = 1.1;

export function estimate({ cores, memoryMb }) {
  const usableCores = Math.max(0, cores - 1);
  const usableMb = Math.floor(memoryMb * 0.75);
  const byCpu = Math.floor(usableCores / 0.4);
  const byRam = Math.floor(usableMb / 80);
  const streams = Math.max(0, Math.min(byCpu, byRam));
  const viewerRam = Math.max(0, usableMb - streams * 80);
  const byViewerCpu = Math.floor(usableCores / 0.02);
  const viewers = streams === 0 ? 0 : Math.min(Math.floor(viewerRam / 12), streams * 40, byViewerCpu);
  return { streams, viewers };
}

function command(bin, args) {
  try {
    return execFileSync(bin, args, { encoding: 'utf8', timeout: 4000 });
  } catch (error) {
    return error instanceof Error ? error.message : 'no disponible';
  }
}

function listening(port) {
  const text = command('ss', ['-lnt']);
  return text.includes(`:${port}`);
}

function diskFreeGb(target) {
  const text = command('df', ['-BG', '-P', target]);
  const line = text.split('\n').find((item) => item && !item.startsWith('Filesystem'));
  const field = line?.split(/\s+/)[3]?.replace('G', '');
  const value = Number(field);
  return Number.isFinite(value) ? value : null;
}

function report() {
  const cores = availableParallelism();
  const memoryMb = Math.floor(Number(/MemTotal:\s+(\d+)/.exec(readFileSync('/proc/meminfo', 'utf8'))?.[1] ?? 0) / 1024);
  const liveDisk = diskFreeGb('/var/lib/situ/live');
  const rootDisk = diskFreeGb('/');
  const disk = liveDisk ?? rootDisk;
  const capacity = estimate({ cores, memoryMb });
  const hours = disk === null || capacity.streams === 0 ? null : Math.floor(disk / (RECORDING_GB_PER_HOUR * capacity.streams));
  const lines = [
    `Núcleos: ${cores}`,
    `Memoria: ${memoryMb} MB`,
    `Disco libre: ${disk === null ? 'no se pudo leer' : `${disk} GB`}${liveDisk === null ? ' (en /)' : ' (en /var/lib/situ/live)'}`,
    `Puerto 1936 en escucha local: ${listening(1936) ? 'sí' : 'no'}`,
    `Puerto 443 en escucha local: ${listening(443) ? 'sí' : 'no'}`,
    `ufw:`,
    command('ufw', ['status']),
    'Alcance desde fuera: este script corre en el servidor y no puede ver el firewall del proveedor.',
    'Comprueba 1936/tcp y 443/tcp con nc desde otra red. Un intento al IP público desde la misma máquina suele fallar por hairpin.',
    `Estimación 720p (2.5 Mbps + 128 kbps, sin transcodificar, reservando 1 núcleo y 25% de RAM): ${capacity.streams} transmisiones y ${capacity.viewers} espectadores HLS a la vez.`,
    `Cada espectador sale a unos ${VIEWER_EGRESS_MBPS} Mbps. ${capacity.viewers} espectadores son unos ${(capacity.viewers * VIEWER_EGRESS_MBPS).toFixed(0)} Mbps de salida.`,
    hours === null
      ? 'Horas de grabación: no calculadas.'
      : `Grabación: cerca de ${hours} horas si las ${capacity.streams} transmisiones quedan guardadas a ${RECORDING_GB_PER_HOUR} GB/h cada una. MediaMTX no borra solo.`,
  ];
  process.stdout.write(`${lines.join('\n')}\n`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  report();
}
