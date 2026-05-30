import { useCallback, useEffect, useRef, useState } from 'react';
import styles from './DuckScene.module.css';

const NODES = [
  { id: 'node-1', x: 16 },
  { id: 'node-2', x: 214 },
  { id: 'node-3', x: 412 },
];
const NODE_W = 172;
const NODE_H = 74;
const NODE_Y = 40;
const CY = NODE_Y + NODE_H / 2;

const JOBS = [
  { name: 'daily-revenue', meta: '0 2 * * *', delay: '0s' },
  { name: 'import-feed', meta: 'running', delay: '1.15s' },
  { name: 'flaky-report', meta: '12 ✓ 1 ✗', delay: '2.3s' },
];

type Sim = { online: boolean[]; leader: number; log: string[] };

const INITIAL: Sim = {
  online: [true, true, true],
  leader: 0,
  log: ['node-1 elected · lease 15s', 'cluster online · 3 nodes'],
};

const name = (i: number) => `node-${i + 1}`;
const pick = <T,>(arr: T[]): T => arr[Math.floor(Math.random() * arr.length)];
const indicesWhere = (online: boolean[], want: boolean) =>
  online.map((o, i) => (o === want ? i : -1)).filter((i) => i >= 0);

function step(prev: Sim): Sim {
  const online = [...prev.online];
  const offline = indicesWhere(online, false);
  let leader = prev.leader;
  let msg: string;

  if (offline.length > 0) {
    // a downed node rejoins — re-run the election across everyone online
    const back = pick(offline);
    online[back] = true;
    leader = pick(indicesWhere(online, true));
    msg = `${name(back)} rejoined · ${name(leader)} now leader`;
  } else {
    // everyone is up — take a random node offline
    const victim = pick(indicesWhere(online, true));
    online[victim] = false;
    if (victim === leader) {
      leader = pick(indicesWhere(online, true));
      msg = `${name(victim)} offline · ${name(leader)} now leader`;
    } else {
      msg = `${name(victim)} offline · ${name(leader)} still leads`;
    }
  }
  return { online, leader, log: [msg, prev.log[0]] };
}

export default function DuckScene() {
  const [sim, setSim] = useState<Sim>(INITIAL);
  const timer = useRef<number | undefined>(undefined);
  const reduced = useRef(false);

  const tick = useCallback(() => {
    setSim(step);
    timer.current = window.setTimeout(tick, 2600 + Math.random() * 1600);
  }, []);

  useEffect(() => {
    reduced.current = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;
    if (reduced.current) return;
    timer.current = window.setTimeout(tick, 2800);
    return () => window.clearTimeout(timer.current);
  }, [tick]);

  const promote = (i: number) => {
    setSim((prev) => {
      const online = [...prev.online];
      online[i] = true;
      return { online, leader: i, log: [`${name(i)} promoted by hand`, prev.log[0]] };
    });
    // give the manual choice a moment before the sim churns again
    if (!reduced.current) {
      window.clearTimeout(timer.current);
      timer.current = window.setTimeout(tick, 3600);
    }
  };

  const meshOpacity = (a: number, b: number) => (sim.online[a] && sim.online[b] ? 1 : 0.2);

  return (
    <svg
      className={styles.scene}
      viewBox="0 0 600 400"
      role="img"
      aria-label="A live DuckRun cluster: nodes drop offline and rejoin while leadership re-elects, with each event narrated in the console."
      preserveAspectRatio="xMidYMid meet"
    >
      {/* header row */}
      <circle cx="22" cy="15" r="4" className={styles.liveDot} />
      <text x="34" y="19" className={styles.kicker}>duckrun · cluster</text>
      <text x="584" y="19" textAnchor="end" className={styles.kickerDim}>
        leader election · lua + ttl
      </text>

      {/* mesh between nodes */}
      <g>
        <line x1={16 + NODE_W} y1={CY} x2={214} y2={CY} className={styles.mesh} style={{ opacity: meshOpacity(0, 1) }} />
        <line x1={214 + NODE_W} y1={CY} x2={412} y2={CY} className={styles.mesh} style={{ opacity: meshOpacity(1, 2) }} />
        <circle cx={(16 + NODE_W + 214) / 2} cy={CY} r="2.5" className={styles.meshDot} style={{ opacity: meshOpacity(0, 1) }} />
        <circle cx={(214 + NODE_W + 412) / 2} cy={CY} r="2.5" className={styles.meshDot} style={{ opacity: meshOpacity(1, 2) }} />
      </g>

      {/* nodes */}
      {NODES.map((n, i) => {
        const isOnline = sim.online[i];
        const isLeader = isOnline && sim.leader === i;
        const cardClass = !isOnline ? styles.cardOffline : isLeader ? styles.cardLeader : styles.card;
        return (
          <g
            key={n.id}
            className={`${styles.node} ${isOnline ? '' : styles.nodeOffline}`}
            onClick={() => promote(i)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' || e.key === ' ') promote(i);
            }}
            role="button"
            tabIndex={0}
            aria-label={`Promote ${n.id} to leader`}
          >
            <rect x={n.x} y={NODE_Y} width={NODE_W} height={NODE_H} rx="8" className={cardClass} />
            {isLeader && <circle cx={n.x + 22} cy={NODE_Y + 24} r="10" className={styles.ledGlow} />}
            <circle
              cx={n.x + 22}
              cy={NODE_Y + 24}
              r="5"
              className={!isOnline ? styles.ledDown : isLeader ? styles.ledOn : styles.ledOff}
            />
            <text x={n.x + 40} y={NODE_Y + 28} className={styles.nodeName}>
              {n.id}
            </text>
            <text x={n.x + 22} y={NODE_Y + 52} className={styles.nodeMeta}>
              {!isOnline ? 'offline' : isLeader ? 'leader · 15s lease' : 'follower'}
            </text>
            <g className={styles.bars} transform={`translate(${n.x + NODE_W - 48}, ${NODE_Y + 24})`}>
              <rect x="0" y="-2" width="4" height="9" rx="1" />
              <rect x="8" y="-7" width="4" height="14" rx="1" />
              <rect x="16" y="-4" width="4" height="11" rx="1" />
              <rect x="24" y="-9" width="4" height="16" rx="1" />
              <rect x="32" y="-3" width="4" height="10" rx="1" />
            </g>
          </g>
        );
      })}

      {/* jobs panel */}
      <rect x="16" y="150" width="568" height="124" rx="10" className={styles.panel} />
      <text x="34" y="175" className={styles.kicker}>JOBS</text>
      <text x="566" y="175" textAnchor="end" className={styles.kickerDim}>
        cluster-wide concurrency
      </text>
      {JOBS.map((j, i) => {
        const y = 202 + i * 24;
        return (
          <g key={j.name} className={styles.jobRow}>
            <rect x="24" y={y - 13} width="552" height="22" rx="4" className={styles.jobHit} />
            <text x="34" y={y + 1} className={styles.jobName}>
              {j.name}
            </text>
            <line x1="196" y1={y - 3} x2="470" y2={y - 3} className={styles.track} />
            <rect
              x="196"
              y={y - 8}
              width="14"
              height="10"
              rx="3"
              className={styles.packet}
              style={{ animationDelay: j.delay }}
            />
            <text x="566" y={y + 1} textAnchor="end" className={styles.jobMeta}>
              {j.meta}
            </text>
          </g>
        );
      })}

      {/* console — narrates the cluster */}
      <rect x="16" y="290" width="568" height="92" rx="10" className={styles.console} />
      <text x="34" y="313" className={styles.cKicker}>console · cluster events</text>
      <text x="34" y="340" className={styles.cLine}>
        › {sim.log[0]}
      </text>
      <text x="34" y="362" className={styles.cLineDim}>
        {sim.log[1]}
      </text>
      <rect x="34" y="370" width="8" height="3" className={styles.cursor} />
    </svg>
  );
}
