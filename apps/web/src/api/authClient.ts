import { api } from './client'

export const authClient = {
  login: (email: string, password: string) => api.post<{ token: string; email: string; refreshToken: string }>('/auth/login', { email, password }),
  refresh: (refreshToken: string) => api.post<{ token: string; email: string; refreshToken: string }>('/auth/refresh', { refreshToken }),
  logout: (refreshToken: string) => api.post('/auth/logout', { refreshToken }),
  deleteAccount: () => api.delete('/auth/account'),
}
