import { DatePipe } from '@angular/common';
import { Component, inject, input, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Estado, NOMBRE_CANAL, NOMBRE_ESTADO, NOMBRE_PRIORIDAD, ReclamoDetalle } from '../modelos';
import { mensajeDeError } from '../servicios/interceptor';
import { ReclamosApi } from '../servicios/reclamos-api';

@Component({
  selector: 'app-detalle',
  imports: [FormsModule, RouterLink, DatePipe],
  template: `
    <a routerLink="/reclamos" class="volver">← Volver a la lista</a>
    @if (error()) { <p class="error">{{ error() }}</p> }

    @if (detalle(); as d) {
      <div class="encabezado">
        <h1><span class="mono">{{ d.reclamo.numero }}</span> · {{ d.reclamo.asunto }}</h1>
        <span class="badge grande estado-{{ d.reclamo.estado }}">{{ nombreEstado[d.reclamo.estado] }}</span>
      </div>

      <div class="columnas">
        <section class="tarjeta">
          <h2>Datos</h2>
          <dl>
            <dt>Cliente</dt><dd>{{ d.reclamo.clienteNombre }} <span class="sutil">({{ d.reclamo.clienteCuit }})</span></dd>
            <dt>Prioridad</dt><dd><span class="badge prioridad-{{ d.reclamo.prioridad }}">{{ nombrePrioridad[d.reclamo.prioridad] }}</span></dd>
            <dt>Canal</dt><dd>{{ nombreCanal[d.reclamo.canal] }}</dd>
            <dt>Alta</dt><dd>{{ d.reclamo.fechaAlta | date: 'dd/MM/yyyy HH:mm' }}</dd>
            <dt>Vence</dt>
            <dd [class.vencido]="d.reclamo.vencido">
              {{ d.reclamo.fechaLimite | date: 'dd/MM/yyyy HH:mm' }} @if (d.reclamo.vencido) { (vencido) }
            </dd>
            <dt>Asignado a</dt><dd>{{ d.reclamo.asignadoA ?? 'Sin asignar' }}</dd>
            @if (d.reclamo.fechaCierre) { <dt>Cierre</dt><dd>{{ d.reclamo.fechaCierre | date: 'dd/MM/yyyy HH:mm' }}</dd> }
          </dl>
          <h3>Descripción</h3>
          <p class="descripcion">{{ d.descripcion }}</p>
        </section>

        <section class="tarjeta">
          <h2>Acciones</h2>
          @if (d.estadosSiguientes.length) {
            <label>Comentario
              <textarea rows="3" [(ngModel)]="comentario" placeholder="Opcional, salvo para rechazar"></textarea>
            </label>
            <div class="botones">
              @for (e of d.estadosSiguientes; track e) {
                <button [class.peligro]="e === 'Rechazado'" [disabled]="enviando()" (click)="cambiar(e)">Pasar a {{ nombreEstado[e] }}</button>
              }
              <button class="secundario" [disabled]="!comentario.trim() || enviando()" (click)="comentar()">Solo comentar</button>
            </div>
          } @else {
            <p class="sutil">El reclamo está {{ nombreEstado[d.reclamo.estado].toLowerCase() }}: no admite más cambios.</p>
          }
          @if (aviso()) { <p class="error">{{ aviso() }}</p> }

          <h2>Historial</h2>
          <ol class="historial">
            @for (m of d.historial; track $index) {
              <li>
                <div class="sutil chico">{{ m.fecha | date: 'dd/MM/yyyy HH:mm' }} · {{ m.usuario }}</div>
                @if (m.estadoNuevo) {
                  <div>
                    @if (m.estadoAnterior) { {{ nombreEstado[m.estadoAnterior] }} → }
                    <strong>{{ nombreEstado[m.estadoNuevo] }}</strong>
                  </div>
                }
                @if (m.comentario) { <div class="comentario">{{ m.comentario }}</div> }
              </li>
            }
          </ol>
        </section>
      </div>
    }
  `,
})
export class DetallePagina implements OnInit {
  readonly numero = input.required<string>();
  private api = inject(ReclamosApi);
  protected nombreEstado = NOMBRE_ESTADO;
  protected nombrePrioridad = NOMBRE_PRIORIDAD;
  protected nombreCanal = NOMBRE_CANAL;
  protected detalle = signal<ReclamoDetalle | null>(null);
  protected error = signal('');
  protected aviso = signal('');
  protected enviando = signal(false);
  protected comentario = '';

  ngOnInit() {
    this.api.obtener(this.numero()).subscribe({
      next: (d) => this.detalle.set(d),
      error: (e) => this.error.set(mensajeDeError(e)),
    });
  }

  cambiar(estado: Estado) {
    this.enviar(this.api.cambiarEstado(this.numero(), estado, this.comentario.trim() || null));
  }

  comentar() {
    this.enviar(this.api.comentar(this.numero(), this.comentario.trim()));
  }

  private enviar(pedido: ReturnType<ReclamosApi['obtener']>) {
    this.enviando.set(true);
    this.aviso.set('');
    pedido.subscribe({
      next: (d) => {
        this.detalle.set(d);
        this.comentario = '';
        this.enviando.set(false);
      },
      error: (e) => {
        this.aviso.set(mensajeDeError(e));
        this.enviando.set(false);
      },
    });
  }
}
