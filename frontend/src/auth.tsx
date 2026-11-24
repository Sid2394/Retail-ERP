import { createContext, ReactNode, useContext, useState } from 'react';
import { api, clearLookups } from './api';

export interface AuthUser { id: number; username: string; fullName: string; role: string; permissions: string[] }
interface Ctx { user: AuthUser | null; login: (u: string, p: string) => Promise<void>; logout: () => void; can: (perm?: string) => boolean }
const AuthContext = createContext<Ctx>(null as any);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(() => { try { return JSON.parse(localStorage.getItem('user') ?? 'null'); } catch { return null; } });
  const login = async (username: string, password: string) => {
    const { data } = await api.post('/auth/login', { username, password });
    localStorage.setItem('token', data.token); localStorage.setItem('user', JSON.stringify(data.user)); setUser(data.user);
  };
  const logout = () => { api.post('/auth/logout').catch(() => {}); localStorage.removeItem('token'); localStorage.removeItem('user'); clearLookups(); setUser(null); };
  const can = (perm?: string) => !perm || !!user?.permissions.includes(perm);
  return <AuthContext.Provider value={{ user, login, logout, can }}>{children}</AuthContext.Provider>;
}
export const useAuth = () => useContext(AuthContext);
