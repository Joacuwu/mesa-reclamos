import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Cliente, Estado, FilaSla, Pagina, Prioridad, ReclamoDetalle, ReclamoResumen } from '../modelos';

export interface Filtro {
  estado?: Estado | '';
  prioridad?: Prioridad | '';
  texto?: string;
  vencidos?: boolean;
  pagina?: number;
}

@Injectable({ providedIn: 'root' })
export class ReclamosApi {
  private http = inject(HttpClient);

  buscar(f: Filtro) {
    let params = new HttpParams().set('pagina', f.pagina ?? 1).set('tamano', 10);
    if (f.estado) params = params.set('estado', f.estado);
    if (f.prioridad) params = params.set('prioridad', f.prioridad);
    if (f.texto?.trim()) params = params.set('texto', f.texto.trim());
    if (f.vencidos) params = params.set('vencidos', true);
    return this.http.get<Pagina<ReclamoResumen>>('/api/reclamos', { params });
  }

  obtener(numero: string) {
    return this.http.get<ReclamoDetalle>(`/api/reclamos/${encodeURIComponent(numero)}`);
  }

  crear(datos: { clienteCuit: string; asunto: string; descripcion: string; prioridad: Prioridad; canal: string }) {
    return this.http.post<ReclamoDetalle>('/api/reclamos', datos);
  }

  cambiarEstado(numero: string, estado: Estado, comentario: string | null) {
    return this.http.post<ReclamoDetalle>(`/api/reclamos/${encodeURIComponent(numero)}/estado`, { estado, comentario });
  }

  comentar(numero: string, comentario: string) {
    return this.http.post<ReclamoDetalle>(`/api/reclamos/${encodeURIComponent(numero)}/comentarios`, { comentario });
  }

  clientes(texto: string) {
    return this.http.get<Cliente[]>('/api/clientes', { params: { texto } });
  }

  reporteSla(desde: string, hasta: string) {
    return this.http.get<FilaSla[]>('/api/reportes/sla', { params: { desde, hasta } });
  }
}
