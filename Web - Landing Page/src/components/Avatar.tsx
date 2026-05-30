import styles from './Avatar.module.css';

type Props = {
  name: string;
  initials: string;
  /** Drop a path (e.g. an image in /public) to replace the monogram template. */
  photo?: string | null;
  size?: number;
};

export default function Avatar({ name, initials, photo, size = 92 }: Props) {
  const style = { width: size, height: size };

  if (photo) {
    return (
      <img
        className={styles.photo}
        style={style}
        src={photo}
        alt={name}
        width={size}
        height={size}
        loading="lazy"
      />
    );
  }

  return (
    <div className={styles.mono} style={style} role="img" aria-label={name}>
      <span>{initials}</span>
    </div>
  );
}
