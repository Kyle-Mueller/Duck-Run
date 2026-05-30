type Props = {
  size?: number;
  /** Eye knockout colour — should match the surface behind the mark. */
  eyeColor?: string;
  className?: string;
};

/**
 * DuckRun wordmark glyph — a duck head facing right with a coral bill.
 * Head uses currentColor so it inherits text colour; pass eyeColor to match
 * the surface (cream on light, ink on dark).
 */
export default function DuckMark({ size = 26, eyeColor = 'var(--bg)', className }: Props) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 32 32"
      fill="none"
      aria-hidden="true"
      focusable="false"
      className={className}
    >
      <circle cx="14.5" cy="15.5" r="8.4" fill="currentColor" />
      <path
        d="M21.6 12.8h7.2c1.4 0 2.1 1.6 1.2 2.7l-.9 1.1c-.35.45-.9.7-1.45.7H21.6z"
        fill="var(--brand)"
      />
      <circle cx="12.1" cy="13.1" r="1.55" fill={eyeColor} />
    </svg>
  );
}
