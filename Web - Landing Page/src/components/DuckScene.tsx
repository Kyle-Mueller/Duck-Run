import { useState } from 'react';
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

export default function DuckScene() {
  const [leader, setLeader] = useState(0);

  return (
    <svg
      className={styles.scene}
      viewBox="0 0 600 400"
      role="img"
      aria-label="A DuckRun cluster: three nodes with one elected leader, scheduled jobs dispatching runs, and a live per-run console."
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
        <line x1={16 + NODE_W} y1={CY} x2={214} y2={CY} className={styles.mesh} />
        <line x1={214 + NODE_W} y1={CY} x2={412} y2={CY} className={styles.mesh} />
        <circle cx={(16 + NODE_W + 214) / 2} cy={CY} r="2.5" className={styles.meshDot} />
        <circle cx={(214 + NODE_W + 412) / 2} cy={CY} r="2.5" className={styles.meshDot} />
      </g>

      {/* nodes */}
      {NODES.map((n, i) => {
        const isLeader = leader === i;
        return (
          <g
            key={n.id}
            className={styles.node}
            onClick={() => setLeader(i)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' || e.key === ' ') setLeader(i);
            }}
            role="button"
            tabIndex={0}
            aria-label={`Make ${n.id} the leader`}
          >
            <rect
              x={n.x}
              y={NODE_Y}
              width={NODE_W}
              height={NODE_H}
              rx="8"
              className={isLeader ? styles.cardLeader : styles.card}
            />
            {isLeader && <circle cx={n.x + 22} cy={NODE_Y + 24} r="10" className={styles.ledGlow} />}
            <circle
              cx={n.x + 22}
              cy={NODE_Y + 24}
              r="5"
              className={isLeader ? styles.ledOn : styles.ledOff}
            />
            <text x={n.x + 40} y={NODE_Y + 28} className={styles.nodeName}>
              {n.id}
            </text>
            <text x={n.x + 22} y={NODE_Y + 52} className={styles.nodeMeta}>
              {isLeader ? 'leader · 15s lease' : 'follower'}
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

      {/* console */}
      <rect x="16" y="290" width="568" height="92" rx="10" className={styles.console} />
      <text x="34" y="313" className={styles.cKicker}>console · run 9f3a2c</text>
      <text x="34" y="340" className={styles.cLine}>
        › import-feed started — leader node-{leader + 1}
      </text>
      <text x="34" y="362" className={styles.cLineDim}>
        1,204 rows fetched — ok (412 ms)
      </text>
      <rect x="250" y="352" width="8" height="12" className={styles.cursor} />
    </svg>
  );
}
