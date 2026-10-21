import bankHomeJson from "../../../fixtures/bank-home.json";
import type { BankHome } from "../types/bank";

export function getBankHome(): BankHome {
  return bankHomeJson as BankHome;
}
