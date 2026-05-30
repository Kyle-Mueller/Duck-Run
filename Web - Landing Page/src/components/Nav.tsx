import { useState } from 'react';
import { Link, NavLink } from 'react-router-dom';
import DuckMark from './DuckMark';
import { SITE } from '../site';
import styles from './Nav.module.css';

export default function Nav() {
  const [open, setOpen] = useState(false);
  const close = () => setOpen(false);

  return (
    <header className={styles.nav}>
      <div className={`container ${styles.inner}`}>
        <Link to="/" className={styles.brand} onClick={close}>
          <DuckMark size={26} />
          <span className={styles.word}>DuckRun</span>
          <span className={styles.ver}>{SITE.version}</span>
        </Link>

        <nav className={`${styles.links} ${open ? styles.open : ''}`}>
          <Link to="/#packages" className={styles.link} onClick={close}>
            Packages
          </Link>
          <Link to="/#architecture" className={styles.link} onClick={close}>
            How it works
          </Link>
          <span className={styles.divider} aria-hidden="true" />
          <a className={styles.link} href={SITE.github} target="_blank" rel="noreferrer">
            GitHub ↗
          </a>
          <NavLink
            to="/docs"
            className={({ isActive }) =>
              isActive ? `${styles.link} ${styles.active}` : styles.link
            }
            onClick={close}
          >
            Docs
          </NavLink>
          <Link to="/docs#installation" className={`btn btn--primary ${styles.cta}`} onClick={close}>
            Get started
          </Link>
        </nav>

        <button
          type="button"
          className={`${styles.burger} ${open ? styles.burgerOpen : ''}`}
          aria-label="Toggle menu"
          aria-expanded={open}
          onClick={() => setOpen((o) => !o)}
        >
          <span />
          <span />
        </button>
      </div>
    </header>
  );
}
