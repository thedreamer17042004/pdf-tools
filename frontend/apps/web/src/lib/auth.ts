"use client";

import { create } from "zustand";
import { persist } from "zustand/middleware";
import { getApiBase } from "@/lib/api";

export type AuthUser = {
  userId: string;
  email: string;
  displayName?: string | null;
  accessToken: string;
  expiresAtUtc: string;
};

type AuthState = {
  user: AuthUser | null;
  setUser: (user: AuthUser | null) => void;
  logout: () => void;
};

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      setUser: (user) => set({ user }),
      logout: () => set({ user: null }),
    }),
    { name: "nam-dev-auth" },
  ),
);

type AuthApiResponse = {
  userId: string;
  email: string;
  displayName?: string | null;
  accessToken: string;
  expiresAtUtc: string;
};

function toAuthUser(raw: AuthApiResponse): AuthUser {
  return {
    userId: raw.userId,
    email: raw.email,
    displayName: raw.displayName,
    accessToken: raw.accessToken,
    expiresAtUtc: raw.expiresAtUtc,
  };
}

export async function register(email: string, password: string, displayName?: string) {
  const res = await fetch(`${getApiBase()}/api/v1/auth/register`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password, displayName }),
  });
  if (!res.ok) throw new Error(await readError(res));
  return toAuthUser(await res.json());
}

export async function login(email: string, password: string) {
  const res = await fetch(`${getApiBase()}/api/v1/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
  });
  if (!res.ok) throw new Error(await readError(res));
  return toAuthUser(await res.json());
}

export function authHeaders(token?: string | null): HeadersInit {
  return token ? { Authorization: `Bearer ${token}` } : {};
}

async function readError(res: Response) {
  try {
    const json = await res.json();
    return json?.error?.message ?? `Request failed (${res.status})`;
  } catch {
    return `Request failed (${res.status})`;
  }
}
