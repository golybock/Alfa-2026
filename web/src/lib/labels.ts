import type { AnalysisStatus, Confidence, CriticStatus, EvidenceType, ReviewStatus, Role } from "./types/api";

export const roleLabels: Record<Role, string> = {
  ceo: "Генеральный директор",
  cfo: "Финансовый директор",
};

export const roleShortLabels: Record<Role, string> = {
  ceo: "Ген. директор",
  cfo: "Фин. директор",
};

export const roleEmphasis: Record<Role, string> = {
  ceo: "здесь акцент на операционных решениях, спросе и договорах",
  cfo: "здесь акцент на проверяемых вопросах к финансированию и затратам",
};

export const evidenceTypeLabels: Record<EvidenceType, string> = {
  news: "Новость",
  bank_research: "Аналитика банка",
  company_feature: "Данные компании",
  payment_aggregate: "Платёжный агрегат",
  group_feature: "Данные группы",
};

export const reviewStatusLabels: Record<ReviewStatus, string> = {
  not_reviewed: "Не проверено вручную",
  partially_reviewed: "Проверено вручную частично",
  reviewed: "Проверено вручную",
};

export const criticStatusLabels: Record<CriticStatus, string> = {
  passed: "Проверено критиком",
  issues_found: "Критик нашёл замечания",
  skipped_disabled: "Без проверки критиком: проверка отключена",
  skipped_budget: "Без проверки критиком: не хватило бюджета",
  skipped_error: "Без проверки критиком: ошибка проверки",
};

export const criticLineLabels: Record<CriticStatus, string> = {
  passed: "Критик проверил вывод",
  issues_found: "Критик нашёл замечания",
  skipped_disabled: "Критик не запускался: проверка отключена",
  skipped_budget: "Критик не запускался: не хватило бюджета",
  skipped_error: "Критик не запускался: ошибка проверки",
};

export const confidenceLabels: Record<Confidence, string> = {
  low: "низкая",
  medium: "средняя",
  high: "высокая",
};

export const pendingAnalysisLabels: Record<Exclude<AnalysisStatus, "ready">, string> = {
  queued: "Полная карточка в очереди на подготовку",
  processing: "Полная карточка готовится",
  failed: "Полный анализ не удался",
};

const contributionLabels: Record<string, string> = {
  okved: "Отрасль по ОКВЭД",
  factor: "Чувствительность отрасли к фактору",
  product: "Продукт компании в банке",
  country: "Страна ФТС",
  cp: "Платежи с компаниями затронутой отрасли",
  group: "Компании группы",
};

export function contributionLabel(code: string): string {
  return contributionLabels[code] ?? code;
}
