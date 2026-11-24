import { FormEvent, useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { errMsg } from '../api';
import { useAuth } from '../auth';

export default function Login() {
  const { user, login } = useAuth(); const nav = useNavigate();
  const [username, setU] = useState(''); const [password, setP] = useState(''); const [err, setErr] = useState(''); const [busy, setBusy] = useState(false);
  if (user) return <Navigate to="/dashboard" replace />;
  const submit = async (e: FormEvent) => { e.preventDefault(); setBusy(true); setErr(''); try { await login(username, password); nav('/dashboard'); } catch (x) { setErr(errMsg(x)); } finally { setBusy(false); } };
  return (
    <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-brand-50 to-slate-100 p-4">
      <form onSubmit={submit} className="card w-full max-w-sm p-8">
        <div className="mb-6 text-center"><div className="text-3xl">🏭</div><h1 className="mt-2 text-xl font-bold text-slate-900">MiniERP</h1><p className="text-sm text-slate-500">JAI BADRI KEDAR TRADERS</p></div>
        <label className="label">Username</label><input className="input mb-3" autoFocus value={username} onChange={(e) => setU(e.target.value)} />
        <label className="label">Password</label><input className="input mb-4" type="password" value={password} onChange={(e) => setP(e.target.value)} />
        {err && <div className="mb-3 rounded-md bg-red-50 p-2 text-sm text-red-700">{err}</div>}
        <button className="btn btn-primary w-full" disabled={busy || !username || !password}>{busy ? 'Signing in…' : 'Sign in'}</button>
      </form>
    </div>
  );
}
