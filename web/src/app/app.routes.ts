import { Routes } from '@angular/router';
import { DetallePagina } from './paginas/detalle';
import { IngresarPagina } from './paginas/ingresar';
import { ListaPagina } from './paginas/lista';
import { NuevoPagina } from './paginas/nuevo';
import { ReportePagina } from './paginas/reporte';
import { conSesion, soloSupervisor } from './servicios/guardias';

export const routes: Routes = [
  { path: 'ingresar', component: IngresarPagina, title: 'Ingresar · Mesa de Reclamos' },
  { path: 'reclamos', component: ListaPagina, canActivate: [conSesion], title: 'Reclamos' },
  { path: 'reclamos/nuevo', component: NuevoPagina, canActivate: [conSesion], title: 'Nuevo reclamo' },
  { path: 'reclamos/:numero', component: DetallePagina, canActivate: [conSesion], title: 'Reclamo' },
  { path: 'reporte', component: ReportePagina, canActivate: [conSesion, soloSupervisor], title: 'Reporte de SLA' },
  { path: '', pathMatch: 'full', redirectTo: 'reclamos' },
  { path: '**', redirectTo: 'reclamos' },
];
