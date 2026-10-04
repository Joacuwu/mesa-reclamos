import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged, filter, switchMap } from 'rxjs';
import { Cliente, NOMBRE_PRIORIDAD, Prioridad } from '../modelos';
import { mensajeDeError } from '../servicios/interceptor';
import { ReclamosApi } from '../servicios/reclamos-api';

/** Mismo algoritmo que Cuit.EsValido en C#: se valida en el navegador y otra vez en el servidor. */
export function cuitValido(control: AbstractControl): ValidationErrors | null {
  const d = String(control.value ?? '').replace(/\D/g, '');
  if (d.length !== 11) return { cuit: true };
  const pesos = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];
  const suma = pesos.reduce((s, p, i) => s + p * Number(d[i]), 0);
  let v = 11 - (suma % 11);
  if (v === 11) v = 0;
  return v !== 10 && v === Number(d[10]) ? null : { cuit: true };
}

@Component({
  selector: 'app-nuevo',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <a routerLink="/reclamos" class="volver">← Volver a la lista</a>
    <h1>Nuevo reclamo</h1>
    <form class="tarjeta formulario" [formGroup]="form" (ngSubmit)="guardar()">
      <label>Buscar cliente
        <input [value]="''" (input)="busqueda.set($any($event.target).value)" placeholder="Razón social o CUIT" />
      </label>
      @if (sugerencias().length) {
        <ul class="sugerencias">
          @for (c of sugerencias(); track c.id) {
            <li><button type="button" (click)="elegir(c)">{{ c.razonSocial }} <span class="sutil">{{ c.cuit }}</span></button></li>
          }
        </ul>
      }
      <label>CUIT del cliente
        <input formControlName="clienteCuit" placeholder="20-12345678-6" />
        @if (form.controls.clienteCuit.touched && form.controls.clienteCuit.invalid) {
          <span class="error chico">El CUIT no es válido.</span>
        }
      </label>
      <label>Asunto <input formControlName="asunto" maxlength="120" /></label>
      <label>Descripción <textarea formControlName="descripcion" rows="4"></textarea></label>
      <div class="fila">
        <label>Prioridad
          <select formControlName="prioridad">
            @for (p of prioridades; track p) { <option [value]="p">{{ nombrePrioridad[p] }}</option> }
          </select>
        </label>
        <label>Canal
          <select formControlName="canal">
            <option value="Web">Web</option>
            <option value="Telefono">Teléfono</option>
            <option value="Email">Email</option>
          </select>
        </label>
      </div>
      @if (error()) { <p class="error">{{ error() }}</p> }
      <button class="primario" [disabled]="form.invalid || enviando()">Crear reclamo</button>
    </form>
  `,
})
export class NuevoPagina {
  private api = inject(ReclamosApi);
  private router = inject(Router);
  protected prioridades: Prioridad[] = ['Critica', 'Alta', 'Media', 'Baja'];
  protected nombrePrioridad = NOMBRE_PRIORIDAD;
  protected form = inject(FormBuilder).nonNullable.group({
    clienteCuit: ['', [Validators.required, cuitValido]],
    asunto: ['', [Validators.required, Validators.maxLength(120)]],
    descripcion: ['', Validators.required],
    prioridad: ['Media' as Prioridad],
    canal: ['Telefono'],
  });
  protected busqueda = signal('');
  protected sugerencias = signal<Cliente[]>([]);
  protected error = signal('');
  protected enviando = signal(false);

  constructor() {
    // Busca clientes mientras se escribe, esperando 300 ms entre teclas para no saturar la API
    toObservable(this.busqueda)
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        filter((t) => t.trim().length >= 2),
        switchMap((t) => this.api.clientes(t.trim())),
        takeUntilDestroyed(),
      )
      .subscribe((c) => this.sugerencias.set(c));
  }

  elegir(c: Cliente) {
    this.form.controls.clienteCuit.setValue(c.cuit);
    this.sugerencias.set([]);
  }

  guardar() {
    this.enviando.set(true);
    this.error.set('');
    this.api.crear(this.form.getRawValue()).subscribe({
      next: (d) => this.router.navigate(['/reclamos', d.reclamo.numero]),
      error: (e) => {
        this.error.set(mensajeDeError(e));
        this.enviando.set(false);
      },
    });
  }
}
