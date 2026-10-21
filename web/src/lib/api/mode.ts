export const USE_MOCKS = process.env.NEXT_PUBLIC_USE_MOCKS === "true";

export function requireMocks() {
  if (!USE_MOCKS) {
    throw new Error("API ещё не подключено: включите NEXT_PUBLIC_USE_MOCKS=true");
  }
}
