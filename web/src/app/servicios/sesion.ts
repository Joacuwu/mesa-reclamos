import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { Sesion } from '../modelos';

const CLAVE = 'mesa-reclamos-sesion';

/** Guarda el JWT en sessionStorage: se borra al cerrar la pestaña. */
@Injectable({ providedIn: 'root' })
export class SesionServicio {
  private http = inject(HttpClient);
  private router = inject(Router);

  readonly sesion = signal<Sesion | null>(this.leer());
  readonly esSupervisor = computed(() => this.sesion()?.rol === 'Supervisor');

  async ingresar(login: string, clave: string): Promise<void> {
    const s = await firstValueFrom(this.http.post<Sesion>('/api/auth/login', { login, clave }));
    sessionStorage.setItem(CLAVE, JSON.stringify(s));
    this.sesion.set(s);
  }

  salir(): void {
    sessionStorage.removeItem(CLAVE);
    this.sesion.set(null);
    this.router.navigate(['/ingresar']);
  }

  private leer(): Sesion | null {
    try {
      const s = JSON.parse(sessionStorage.getItem(CLAVE) ?? 'null') as Sesion | null;
      return s && new Date(s.vence) > new Date() ? s : null;
    } catch {
      return null;
    }
  }
}
