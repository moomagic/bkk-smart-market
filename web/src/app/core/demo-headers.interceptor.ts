import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { DemoSession } from './demo-session';

/** Mock auth: every API call carries the current demo role and user. */
export const demoHeadersInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith('/api/')) return next(req);
  const session = inject(DemoSession);
  return next(
    req.clone({
      setHeaders: { 'X-Demo-Role': session.role(), 'X-Demo-User': session.user() },
    }),
  );
};
