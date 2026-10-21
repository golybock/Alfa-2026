import bankHomeJson from "../../../fixtures/bank-home.json";
import selectionJson from "../../../fixtures/selection.json";
import type { BankHome, SelectionData } from "../types/bank";

export function getBankHome(): BankHome {
  return bankHomeJson as BankHome;
}

export function getSelection(): SelectionData {
  return selectionJson as SelectionData;
}
