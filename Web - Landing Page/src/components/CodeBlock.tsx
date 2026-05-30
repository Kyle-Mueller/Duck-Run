import { useState } from 'react';
import styles from './CodeBlock.module.css';

type Props = {
  code: string;
  lang?: 'csharp' | 'bash' | 'json' | 'yaml' | 'text';
  label?: string;
};

type Cls = 'plain' | 'comment' | 'string' | 'keyword' | 'type' | 'number' | 'attr';
type Tok = { t: string; c: Cls };

const KEYWORDS = new Set([
  'public', 'private', 'protected', 'internal', 'sealed', 'static', 'abstract', 'virtual',
  'override', 'partial', 'readonly', 'const', 'async', 'await', 'var', 'new', 'return', 'void',
  'class', 'struct', 'record', 'interface', 'enum', 'namespace', 'using', 'this', 'base', 'null',
  'true', 'false', 'if', 'else', 'for', 'foreach', 'while', 'do', 'switch', 'case', 'break',
  'continue', 'throw', 'try', 'catch', 'finally', 'init', 'required', 'in', 'out', 'ref', 'params',
  'is', 'as', 'typeof', 'nameof', 'int', 'bool', 'string', 'long', 'double', 'decimal', 'object',
  'byte', 'char', 'float', 'uint', 'short',
]);

const RE_WS = /^\s+/;
const RE_STR = /^(?:\$?@?"(?:[^"\\]|\\.)*"?|'(?:[^'\\]|\\.)*'?|`(?:[^`\\]|\\.)*`?)/;
const RE_NUM = /^\d[\d_]*(?:\.\d+)?/;
const RE_ID = /^[A-Za-z_][A-Za-z0-9_]*/;

function tokenizeLine(line: string, lang: string): Tok[] {
  const out: Tok[] = [];
  const isCs = lang === 'csharp';
  const hashComment = lang === 'bash' || lang === 'yaml';
  let i = 0;
  let prev = '';

  while (i < line.length) {
    const rest = line.slice(i);

    const ws = RE_WS.exec(rest);
    if (ws) { out.push({ t: ws[0], c: 'plain' }); i += ws[0].length; continue; }

    if (isCs && rest.startsWith('//')) { out.push({ t: rest, c: 'comment' }); break; }
    if (hashComment && rest[0] === '#') { out.push({ t: rest, c: 'comment' }); break; }

    const str = RE_STR.exec(rest);
    if (str) { out.push({ t: str[0], c: 'string' }); prev = '"'; i += str[0].length; continue; }

    const num = RE_NUM.exec(rest);
    if (num) { out.push({ t: num[0], c: 'number' }); prev = num[0]; i += num[0].length; continue; }

    const id = RE_ID.exec(rest);
    if (id) {
      const w = id[0];
      let c: Cls = 'plain';
      if (isCs) {
        if (prev === '[') c = 'attr';
        else if (KEYWORDS.has(w)) c = 'keyword';
        else if (/^[A-Z]/.test(w)) c = 'type';
      }
      out.push({ t: w, c });
      prev = w;
      i += w.length;
      continue;
    }

    out.push({ t: rest[0], c: 'plain' });
    prev = rest[0];
    i += 1;
  }
  return out;
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
        <span className={styles.dots} aria-hidden="true">
          <i />
          <i />
          <i />
        </span>
        <span className={styles.label}>{label ?? lang}</span>
        <button type="button" className={styles.copy} onClick={copy} aria-label="Copy code">
          {copied ? 'copied' : 'copy'}
        </button>
      </div>
      <div className={styles.scroll}>
        <div className={styles.codeArea}>
          {lines.map((line, li) => {
            const toks = tokenizeLine(line, lang);
            return (
              <div className={styles.row} key={li}>
                <span className={styles.ln}>{li + 1}</span>
                <code className={styles.code}>
                  {toks.length === 0
                    ? ' '
                    : toks.map((tok, ti) => (
                        <span key={ti} className={styles[tok.c]}>
                          {tok.t}
                        </span>
                      ))}
                </code>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
