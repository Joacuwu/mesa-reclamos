import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Estado, NOMBRE_ESTADO, NOMBRE_PRIORIDAD, Pagina, Prioridad, ReclamoResumen } from '../modelos';
import { mensajeDeError } from '../servicios/interceptor';
import { Filtro, ReclamosApi } from '../servicios/reclamos-api';

@Component({
  selector: 'app-lista',
  imports: [FormsModule, RouterLink, DatePipe],
  template: `
    <div class="encabezado">
      <h1>Reclamos</h1>
      <a class="boton primario" routerLink="/reclamos/nuevo">+ Nuevo reclamo</a>
    </div>

    <form class="filtros" (ngSubmit)="buscar(1)">
      <input name="texto" [(ngModel)]="filtro.texto" placeholder="Número, asunto, cliente o CUIT" />
      <select name="estado" [(ngModel)]="filtro.estado" (change)="buscar(1)">
        <option value="">Todos los estados</option>
        @for (e of estados; track e) { <option [value]="e">{{ nombreEstado[e] }}</option> }
      </select>
      <select name="prioridad" [(ngModel)]="filtro.prioridad" (change)="buscar(1)">
        <option value="">Todas las prioridades</option>
        @for (p of prioridades; track p) { <option [value]="p">{{ nombrePrioridad[p] }}</option> }
      </select>
      <label class="check"><input type="checkbox" name="vencidos" [(ngModel)]="filtro.vencidos" (change)="buscar(1)" /> Solo vencidos</label>
      <button>Buscar</button>
    </form>

    @if (error()) { <p class="error">{{ error() }}</p> }

    @if (pagina(); as p) {
      <div class="tabla-contenedor">
        <table>
          <thead>
            <tr><th>Número</th><th>Cliente</th><th>Asunto</th><th>Prioridad</th><th>Estado</th><th>Vence</th></tr>
          </thead>
          <tbody>
            @for (r of p.items; track r.numero) {
              <tr [routerLink]="['/reclamos', r.numero]" class="fila-link">
                <td class="mono">{{ r.numero }}</td>
                <td>{{ r.clienteNombre }}<div class="sutil chico">{{ r.clienteCuit }}</div></td>
                <td>{{ r.asunto }}</td>
                <td><span class="badge prioridad-{{ r.prioridad }}">{{ nombrePrioridad[r.prioridad] }}</span></td>
                <td><span class="badge estado-{{ r.estado }}">{{ nombreEstado[r.estado] }}</span></td>
                <td [class.vencido]="r.vencido">
                  {{ r.fechaLimite | date: 'dd/MM HH:mm' }}
                  @if (r.vencido) { <div class="chico">Vencido</div> }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="vacio">No hay reclamos con esos filtros.</td></tr>
            }
          </tbody>
        </table>
      </div>
      <div class="paginado">
        <span class="sutil">{{ p.total }} reclamo{{ p.total === 1 ? '' : 's' }}</span>
        <button [disabled]="p.numeroPagina <= 1" (click)="buscar(p.numeroPagina - 1)">Anterior</button>
        <span>Página {{ p.numeroPagina }} de {{ paginas(p) }}</span>
        <button [disabled]="p.numeroPagina >= paginas(p)" (click)="buscar(p.numeroPagina + 1)">Siguiente</button>
      </div>
    }
  `,
})
export class ListaPagina {
  private api = inject(ReclamosApi);
  protected estados = Object.keys(NOMBRE_ESTADO) as Estado[];
  protected prioridades = (Object.keys(NOMBRE_PRIORIDAD) as Prioridad[]).reverse();
  protected nombreEstado = NOMBRE_ESTADO;
  protected nombrePrioridad = NOMBRE_PRIORIDAD;
  protected filtro: Filtro = { texto: '', estado: '', prioridad: '', vencidos: false };
  protected pagina = signal<Pagina<ReclamoResumen> | null>(null);
  protected error = signal('');

  constructor() {
    this.buscar(1);
  }

  buscar(numero: number) {
    this.error.set('');
    this.api.buscar({ ...this.filtro, pagina: numero }).subscribe({
      next: (p) => this.pagina.set(p),
      error: (e) => this.error.set(mensajeDeError(e)),
    });
  }

  paginas(p: Pagina<unknown>) {
    return Math.max(1, Math.ceil(p.total / p.tamanoPagina));
  }
}
