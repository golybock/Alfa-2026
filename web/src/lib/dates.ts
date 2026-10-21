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
