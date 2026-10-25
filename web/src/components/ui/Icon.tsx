import type { IconName } from "../../lib/types/icon";

const paths: Record<IconName, string> = {
  grid: "M4 4h6v6H4zM14 4h6v6h-6zM4 14h6v6H4zM14 14h6v6h-6z",
  sparkle: "M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8zM19 16l.8 2.2L22 19l-2.2.8L19 22l-.8-2.2L16 19l2.2-.8z",
  plus: "M12 5v14M5 12h14",
  document: "M6 3h9l3 3v15H6zM9 10h6M9 14h6M9 18h4",
  users: "M9 11a3 3 0 100-6 3 3 0 000 6zM3 20c0-3.3 2.7-6 6-6s6 2.7 6 6M16 5.5a3 3 0 010 5.5M18 14.5c1.8.8 3 2.7 3 5.5",
  percent: "M19 5L5 19M7 9a2 2 0 100-4 2 2 0 000 4zM17 19a2 2 0 100-4 2 2 0 000 4z",
  trend: "M3 17l6-6 4 4 8-8M15 7h6v6",
  trendDown: "M3 7l6 6 4-4 8 8M15 17h6v-6",
  sliders: "M4 7h10M18 7h2M4 17h2M10 17h10M16 4v6M8 14v6",
  search: "M11 4a7 7 0 100 14 7 7 0 000-14zM20 20l-4-4",
  mail: "M4 6h16v12H4zM4 7l8 6 8-6",
  bell: "M6 17v-6a6 6 0 0112 0v6l1.5 2h-15zM10 21h4",
  briefcase: "M4 8h16v11H4zM9 8V5h6v3M4 13h16",
  panel: "M4 5h16v14H4zM9 5v14",
  close: "M6 6l12 12M18 6L6 18",
  check: "M5 12.5l4.5 4.5L19 7.5",
  checkCircle: "M12 3a9 9 0 100 18 9 9 0 000-18zM8 12.5l3 3 5-6",
  warning: "M12 4L2.5 20h19zM12 10v4.5M12 17v.5",
  arrowUpRight: "M7 17L17 7M9 7h8v8",
  chevronLeft: "M15 6l-6 6 6 6",
  chevronDown: "M6 9l6 6 6-6",
  thumbUp: "M7 11v9H4v-9zM7 11l4-7c1.5 0 2.5 1 2 3l-.5 3H19a1.5 1.5 0 011.5 1.8l-1.3 6A2 2 0 0117.2 19H7",
  thumbDown: "M7 13V4H4v9zM7 13l4 7c1.5 0 2.5-1 2-3l-.5-3H19a1.5 1.5 0 001.5-1.8l-1.3-6A2 2 0 0017.2 5H7",
  eyeOff: "M4 4l16 16M10.5 6.2A9 9 0 0121 12a13 13 0 01-3 3.8M6.5 7.5A13 13 0 003 12c1.5 3 5 6 9 6 1.2 0 2.3-.3 3.3-.7",
  info: "M12 3a9 9 0 100 18 9 9 0 000-18zM12 11v6M12 7.5v.5",
  bank: "M3 10l9-6 9 6zM5 10v8M9.5 10v8M14.5 10v8M19 10v8M3 20h18",
  swap: "M7 7h13M16 3l4 4-4 4M17 17H4M8 13l-4 4 4 4",
  upload: "M12 4v12M7 11l5 5 5-5M5 20h14",
  send: "M5 12h14M13 6l6 6-6 6",
  help: "M9 9a3 3 0 116 0c0 2-3 2.5-3 5M12 18v.5",
  camera: "M4 8h4l1.5-2h5L16 8h4v11H4zM12 16a3 3 0 100-6 3 3 0 000 6z",
};

type IconProps = { name: IconName; className?: string };

export default function Icon({ name, className = "h-6 w-6" }: IconProps) {
  return (
    <svg
      aria-hidden
      viewBox="0 0 24 24"
      className={`shrink-0 ${className}`}
      fill="none"
      stroke="currentColor"
      strokeWidth="1.6"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d={paths[name]} />
    </svg>
  );
}
