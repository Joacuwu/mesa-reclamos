import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { SesionServicio } from './sesion';

/** Agrega el token a cada pedido a la API. Si la API responde 401, la sesión venció: vuelve al login. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const sesion = inject(SesionServicio);
  const token = sesion.sesion()?.token;
  const pedido = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
  return next(pedido).pipe(
    catchError((e: HttpErrorResponse) => {
      if (e.status === 401 && token) sesion.salir();
      return throwError(() => e);
    }),
  );
};

/** Mensaje legible a partir de un error de la API (ProblemDetails). */
export function mensajeDeError(e: unknown): string {
  if (e instanceof HttpErrorResponse) {
    if (e.status === 0) return 'No hay conexión con el servidor.';
    const p = e.error as { detail?: string; title?: string; errors?: Record<string, string[]> } | null;
    if (p?.errors) return Object.values(p.errors).flat().join(' ');
    return p?.detail ?? p?.title ?? `Error ${e.status}`;
  }
  return 'Ocurrió un error inesperado.';
}
