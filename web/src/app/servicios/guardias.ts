import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SesionServicio } from './sesion';

export const conSesion: CanActivateFn = () =>
  inject(SesionServicio).sesion() ? true : inject(Router).createUrlTree(['/ingresar']);

export const soloSupervisor: CanActivateFn = () =>
  inject(SesionServicio).esSupervisor() ? true : inject(Router).createUrlTree(['/reclamos']);
