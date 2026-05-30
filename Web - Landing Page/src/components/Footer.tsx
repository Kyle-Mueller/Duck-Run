import { Link } from 'react-router-dom';
import DuckMark from './DuckMark';
import { SITE, PACKAGES } from '../site';
import styles from './Footer.module.css';

const year = new Date().getFullYear();

export default function Footer() {
  return (
    <footer className={styles.footer}>
      <div className={`container ${styles.grid}`}>
        <div className={styles.brand}>
          <Link to="/" className={styles.brandTop}>
            <DuckMark size={28} eyeColor="var(--bg-dark)" />
            <span className={styles.word}>DuckRun</span>
          </Link>
          <p className={styles.tagline}>
            Attribute-driven background jobs for .NET. One <code>[DuckRunJob]</code>, from
            Framework 4.8 to .NET 10.
          </p>
          <p className={styles.signoff}>
            <span className="statusDot" />
            Built by {SITE.author} — issues answered by a human.
          </p>
        </div>

        <div className={styles.col}>
          <h4 className={styles.colTitle}>Packages</h4>
          {PACKAGES.map((p) => (
            <a key={p.id} href={SITE.nuget(p.id)} target="_blank" rel="noreferrer" className={styles.link}>
              {p.id}
            </a>
          ))}
        </div>

        <div className={styles.col}>
          <h4 className={styles.colTitle}>Docs</h4>
          <Link to="/docs#installation" className={styles.link}>Installation</Link>
          <Link to="/docs#jobs" className={styles.link}>Defining jobs</Link>
          <Link to="/docs#persistence" className={styles.link}>Persistence</Link>
          <Link to="/docs#cluster" className={styles.link}>Clustering</Link>
          <Link to="/docs#reference" className={styles.link}>API reference</Link>
        </div>

        <div className={styles.col}>
          <h4 className={styles.colTitle}>Project</h4>
          <a href={SITE.github} target="_blank" rel="noreferrer" className={styles.link}>GitHub ↗</a>
          <a href={SITE.issues} target="_blank" rel="noreferrer" className={styles.link}>Issues ↗</a>
          <a href={SITE.nugetSearch} target="_blank" rel="noreferrer" className={styles.link}>NuGet ↗</a>
          <Link to="/docs#faq" className={styles.link}>FAQ</Link>
        </div>
      </div>

      <div className={`container ${styles.bottom}`}>
        <span>
          © {year} {SITE.name} · {SITE.license} · v{SITE.version}
        </span>
        <span className={styles.fine}>
          Written in C#, mostly after midnight. Pre-1.0 — the API will move.
        </span>
      </div>
    </footer>
  );
}
