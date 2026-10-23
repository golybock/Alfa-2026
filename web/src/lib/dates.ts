const MOSCOW = "Europe/Moscow";

const dayFormat = new Intl.DateTimeFormat("ru-RU", { timeZone: MOSCOW, day: "numeric" });
const dayMonthFormat = new Intl.DateTimeFormat("ru-RU", { timeZone: MOSCOW, day: "numeric", month: "long" });
const monthKeyFormat = new Intl.DateTimeFormat("ru-RU", { timeZone: MOSCOW, month: "numeric", year: "numeric" });
const dateFormat = new Intl.DateTimeFormat("ru-RU", { timeZone: MOSCOW, day: "numeric", month: "long", year: "numeric" });
const dateTimeFormat = new Intl.DateTimeFormat("ru-RU", {
  timeZone: MOSCOW,
  day: "numeric",
  month: "long",
  hour: "2-digit",
  minute: "2-digit",
});

export function formatDate(value: string): string {
  return dateFormat.format(new Date(value));
}

export function formatDateTime(value: string): string {
  return `${dateTimeFormat.format(new Date(value))} МСК`;
}

export function formatPeriod(from: string, to: string): string {
  const start = new Date(from);
  const end = new Date(to);
  if (monthKeyFormat.format(start) === monthKeyFormat.format(end)) {
    return `${dayFormat.format(start)}–${dayMonthFormat.format(end)}`;
  }
  return `${dayMonthFormat.format(start)} – ${dayMonthFormat.format(end)}`;
}

const timeFormat = new Intl.DateTimeFormat("ru-RU", { timeZone: MOSCOW, hour: "2-digit", minute: "2-digit" });
const dayMonthNumericFormat = new Intl.DateTimeFormat("ru-RU", { timeZone: MOSCOW, day: "2-digit", month: "2-digit" });
const relativeFormat = new Intl.RelativeTimeFormat("ru", { numeric: "always" });

export function formatDateWithYear(value: string): string {
  return dateFormat.format(new Date(value)).replace(" г.", "");
}

export function formatDateTimeWithYear(value: string): string {
  const date = new Date(value);
  return `${dateFormat.format(date).replace(" г.", "")}, ${timeFormat.format(date)}`;
}

export function formatDayMonthNumeric(value: string): string {
  return dayMonthNumericFormat.format(new Date(value));
}

export function formatAgo(fromMs: number, nowMs: number): string {
  const seconds = Math.max(0, Math.round((nowMs - fromMs) / 1000));
  if (seconds < 5) {
    return "только что";
  }
  if (seconds < 60) {
    return relativeFormat.format(-seconds, "second");
  }
  const minutes = Math.floor(seconds / 60);
  return minutes < 60 ? relativeFormat.format(-minutes, "minute") : relativeFormat.format(-Math.floor(minutes / 60), "hour");
}