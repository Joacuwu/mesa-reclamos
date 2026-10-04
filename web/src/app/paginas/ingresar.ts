import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { mensajeDeError } from '../servicios/interceptor';
import { SesionServicio } from '../servicios/sesion';

@Component({
  selector: 'app-ingresar',
  imports: [ReactiveFormsModule],
  template: `
    <div class="ingreso">
      <form class="tarjeta" [formGroup]="form" (ngSubmit)="ingresar()">
        <h1>Mesa de Reclamos</h1>
        <p class="sutil">Ingresá con tu usuario</p>
        <label>Usuario <input formControlName="login" autocomplete="username" /></label>
        <label>Contraseña <input type="password" formControlName="clave" autocomplete="current-password" /></label>
        @if (error()) { <p class="error">{{ error() }}</p> }
        <button class="primario" [disabled]="form.invalid || enviando()">Ingresar</button>
        <p class="sutil chico">Demo: <code>operador</code> o <code>supervisor</code>, contraseña <code>demo1234</code></p>
      </form>
    </div>
  `,
})
export class IngresarPagina {
  private sesion = inject(SesionServicio);
  private router = inject(Router);
  protected form = inject(FormBuilder).nonNullable.group({
    login: ['', Validators.required],
    clave: ['', Validators.required],
  });
  protected error = signal('');
  protected enviando = signal(false);

  async ingresar() {
    this.enviando.set(true);
    this.error.set('');
    try {
      const { login, clave } = this.form.getRawValue();
      await this.sesion.ingresar(login, clave);
      this.router.navigate(['/reclamos']);
    } catch (e) {
      this.error.set(mensajeDeError(e));
    } finally {
      this.enviando.set(false);
    }
  }
}
