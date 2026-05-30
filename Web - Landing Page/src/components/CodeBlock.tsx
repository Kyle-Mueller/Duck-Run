import { useState } from 'react';
import styles from './CodeBlock.module.css';

type Props = {
  code: string;
  lang?: 'csharp' | 'bash' | 'json' | 'yaml' | 'text';
  label?: string;
};

type Tok = { t: string; k: 'plain' | 'comment' | 'string' };

function tokenizeLine(line: string, lang: string): Tok[] {
  const toks: Tok[] = [];
  let buf = '';
  let i = 0;
  const flush = () => {
    if (buf) {
      toks.push({ t: buf, k: 'plain' });
      buf = '';
    }
  };
  const hashComment = lang === 'bash' || lang === 'yaml';
  while (i < line.length) {
    const ch = line[i];
    // line comments
    if (!hashComment && ch === '/' && line[i + 1] === '/') {
      flush();
      toks.push({ t: line.slice(i), k: 'comment' });
      return toks;
    }
    if (hashComment && ch === '#' && (i === 0 || /\s/.test(line[i - 1]))) {
      flush();
      toks.push({ t: line.slice(i), k: 'comment' });
      return toks;
    }
    // strings
    if (ch === '"' || ch === '\'' || ch === '`') {
      flush();
      const quote = ch;
      let s = quote;
      i++;
      while (i < line.length) {
        const c = line[i];
        s += c;
        i++;
        if (c === '\\' && i < line.length) {
          s += line[i];
          i++;
          continue;
        }
        if (c === quote) break;
      }
      toks.push({ t: s, k: 'string' });
      continue;
    }
    buf += ch;
    i++;
  }
  flush();
  return toks;
}

export default function CodeBlock({ code, lang = 'text', label }: Props) {
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(code);
      setCopied(true);
      setTimeout(() => setCopied(false), 1600);
    } catch {
      /* clipboard blocked — no-op */
    }
  };

  const lines = code.replace(/\n$/, '').split('\n');

  return (
    <div className={styles.block}>
      <div className={styles.bar}>
        <span className={styles.label}>{label ?? lang}</span>
        <button type="button" className={styles.copy} onClick={copy} aria-label="Copy code">
          {copied ? 'copied' : 'copy'}
        </button>
      </div>
      <pre className={styles.pre}>
        <code>
          {lines.map((line, li) => (
            <span className={styles.line} key={li}>
              {tokenizeLine(line, lang).map((tok, ti) => (
                <span key={ti} className={styles[tok.k]}>
                  {tok.t}
                </span>
              ))}
              {'\n'}
            </span>
          ))}
        </code>
      </pre>
    </div>
  );
}
