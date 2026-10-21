import type { Api } from "./client";
import { httpApi } from "./http";
import { mockApi } from "./mock";
import { USE_MOCKS } from "./mode";

export const api: Api = USE_MOCKS ? mockApi : httpApi;
