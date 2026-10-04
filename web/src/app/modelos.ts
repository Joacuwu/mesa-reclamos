export type Estado = 'Abierto' | 'EnCurso' | 'EsperandoCliente' | 'Resuelto' | 'Cerrado' | 'Rechazado';
export type Prioridad = 'Baja' | 'Media' | 'Alta' | 'Critica';
export type Canal = 'Web' | 'Telefono' | 'Email' | 'Soap';

export interface Sesion {
  token: string;
  nombre: string;
  rol: 'Operador' | 'Supervisor';
  vence: string;
}

export interface ReclamoResumen {
  numero: string;
  clienteCuit: string;
  clienteNombre: string;
  asunto: string;
  prioridad: Prioridad;
  canal: Canal;
  estado: Estado;
  asignadoA: string | null;
  fechaAlta: string;
  fechaLimite: string;
  fechaCierre: string | null;
  vencido: boolean;
}

export interface Movimiento {
  fecha: string;
  usuario: string;
  estadoAnterior: Estado | null;
  estadoNuevo: Estado | null;
  comentario: string | null;
}

export interface ReclamoDetalle {
  reclamo: ReclamoResumen;
  descripcion: string;
  estadosSiguientes: Estado[];
  historial: Movimiento[];
}

export interface Pagina<T> {
  items: T[];
  total: number;
  numeroPagina: number;
  tamanoPagina: number;
}

export interface Cliente {
  id: number;
  cuit: string;
  razonSocial: string;
  email: string;
  telefono: string | null;
}

export interface FilaSla {
  prioridad: Prioridad;
  total: number;
  resueltos: number;
  dentroDeSla: number;
  vencidos: number;
  horasPromedio: number | null;
}

export const NOMBRE_ESTADO: Record<Estado, string> = {
  Abierto: 'Abierto',
  EnCurso: 'En curso',
  EsperandoCliente: 'Esperando al cliente',
  Resuelto: 'Resuelto',
  Cerrado: 'Cerrado',
  Rechazado: 'Rechazado',
};

export const NOMBRE_PRIORIDAD: Record<Prioridad, string> = {
  Baja: 'Baja',
  Media: 'Media',
  Alta: 'Alta',
  Critica: 'Crítica',
};

export const NOMBRE_CANAL: Record<Canal, string> = {
  Web: 'Web',
  Telefono: 'Teléfono',
  Email: 'Email',
  Soap: 'Integración SOAP',
};
