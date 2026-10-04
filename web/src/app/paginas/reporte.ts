import { DecimalPipe, PercentPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FilaSla, NOMBRE_PRIORIDAD } from '../modelos';
import { mensajeDeError } from '../servicios/interceptor';
import { ReclamosApi } from '../servicios/reclamos-api';

const iso = (d: Date) => d.toISOString().slice(0, 10);

@Component({
  selector: 'app-reporte',
  imports: [FormsModule, DecimalPipe, PercentPipe],
  template: `
    <h1>Cumplimiento del SLA</h1>
    <p class="sutil">Reclamos dados de alta en el período, por prioridad. Los rechazados no cuentan.</p>
    <form class="filtros" (ngSubmit)="cargar()">
      <label>Desde <input type="date" name="desde" [(ngModel)]="desde" /></label>
      <label>Hasta <input type="date" name="hasta" [(ngModel)]="hasta" /></label>
      <button>Ver</button>
    </form>
    @if (error()) { <p class="error">{{ error() }}</p> }

    <div class="indicadores">
      <div class="tarjeta"><div class="numero">{{ totales().total }}</div><div class="sutil">Reclamos</div></div>
      <div class="tarjeta"><div class="numero">{{ totales().resueltos }}</div><div class="sutil">Resueltos</div></div>
      <div class="tarjeta"><div class="numero">{{ totales().cumplimiento | percent }}</div><div class="sutil">Resueltos dentro del SLA</div></div>
      <div class="tarjeta"><div class="numero vencido">{{ totales().vencidos }}</div><div class="sutil">Fuera de término</div></div>
    </div>

    <div class="tabla-contenedor">
      <table>
        <thead>
          <tr><th>Prioridad</th><th>Plazo</th><th>Total</th><th>Resueltos</th><th>Dentro del SLA</th><th>Vencidos</th><th>Horas promedio</th><th>Cumplimiento</th></tr>
        </thead>
        <tbody>
          @for (f of filas(); track f.prioridad) {
            <tr>
              <td><span class="badge prioridad-{{ f.prioridad }}">{{ nombrePrioridad[f.prioridad] }}</span></td>
              <td>{{ plazo[f.prioridad] }} h</td>
              <td>{{ f.total }}</td>
              <td>{{ f.resueltos }}</td>
              <td>{{ f.dentroDeSla }}</td>
              <td [class.vencido]="f.vencidos > 0">{{ f.vencidos }}</td>
              <td>{{ f.horasPromedio === null ? '-' : (f.horasPromedio | number: '1.1-1') }}</td>
              <td>
                <div class="barra"><div [style.width.%]="porcentaje(f)"></div></div>
                <span class="chico">{{ porcentaje(f) / 100 | percent }}</span>
              </td>
            </tr>
          } @empty {
            <tr><td colspan="8" class="vacio">No hay reclamos en ese período.</td></tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class ReportePagina {
  private api = inject(ReclamosApi);
  protected nombrePrioridad = NOMBRE_PRIORIDAD;
  protected plazo = { Critica: 4, Alta: 24, Media: 48, Baja: 72 };
  protected desde = iso(new Date(Date.now() - 30 * 86400000));
  protected hasta = iso(new Date());
  protected filas = signal<FilaSla[]>([]);
  protected error = signal('');
  protected totales = computed(() => {
    const f = this.filas();
    const suma = (k: keyof FilaSla) => f.reduce((s, x) => s + Number(x[k] ?? 0), 0);
    const resueltos = suma('resueltos');
    return {
      total: suma('total'),
      resueltos,
      vencidos: suma('vencidos'),
      cumplimiento: resueltos ? suma('dentroDeSla') / resueltos : 0,
    };
  });

  constructor() {
    this.cargar();
  }

  cargar() {
    this.error.set('');
    this.api.reporteSla(this.desde, this.hasta).subscribe({
      next: (f) => this.filas.set(f),
      error: (e) => this.error.set(mensajeDeError(e)),
    });
  }

  /** Porcentaje de los resueltos que se resolvieron a tiempo. */
  porcentaje(f: FilaSla) {
    return f.resueltos ? Math.round((f.dentroDeSla / f.resueltos) * 100) : 0;
  }
}
