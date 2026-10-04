// Recorrido de punta a punta con Playwright: login, lista, detalle, cambio de estado, alta y reporte.
// Uso: con la API corriendo (dotnet run en src/Reclamos.Api) → node e2e/recorrido.mjs [url]
// Guarda capturas en docs/capturas/.
import { chromium } from 'playwright';

const BASE = process.argv[2] ?? 'http://localhost:5292';
const CAPTURAS = new URL('../docs/capturas/', import.meta.url).pathname;
const ok = (cond, msg) => { if (!cond) throw new Error('Falló: ' + msg); console.log('✓ ' + msg); };

// Completa un campo y confirma que el valor quedó (después de navegar, Angular puede tardar
// un instante en reemplazar la vista y un fill demasiado rápido se pierde)
async function llenar(pagina, selector, valor) {
  for (let i = 0; i < 10; i++) {
    await pagina.fill(selector, valor);
    await pagina.waitForTimeout(50);
    if ((await pagina.inputValue(selector)) === valor) return;
  }
  throw new Error('No se pudo completar ' + selector);
}

const navegador = await chromium.launch();
const pagina = await navegador.newPage({ viewport: { width: 1366, height: 820 }, locale: 'es-AR' });
const errores = [];
pagina.on('pageerror', (e) => errores.push(e.message));

// Login con clave incorrecta y después correcta
await pagina.goto(BASE + '/reclamos');
await pagina.waitForURL('**/ingresar');
ok(true, 'sin sesión redirige al login');
await pagina.fill('input[formcontrolname=login]', 'operador');
await pagina.fill('input[formcontrolname=clave]', 'mala');
await pagina.click('button:has-text("Ingresar")');
await pagina.waitForSelector('.error');
ok((await pagina.textContent('.error')).includes('incorrectos'), 'clave incorrecta muestra el error');
await pagina.screenshot({ path: CAPTURAS + '1-ingreso.png' });
await pagina.fill('input[formcontrolname=clave]', 'demo1234');
await pagina.click('button:has-text("Ingresar")');
await pagina.waitForURL('**/reclamos');
await pagina.waitForSelector('tbody tr.fila-link');
ok((await pagina.locator('tbody tr.fila-link').count()) === 10, 'la lista muestra 10 reclamos por página');
ok((await pagina.textContent('.paginado')).includes('12 reclamos'), 'el total es 12');
ok((await pagina.locator('nav a:has-text("Reporte")').count()) === 0, 'el operador no ve el reporte');
await pagina.screenshot({ path: CAPTURAS + '2-lista.png' });

// Filtro de vencidos
await pagina.check('input[name=vencidos]');
await pagina.waitForFunction(() => document.querySelector('.paginado')?.textContent?.includes('3 reclamos'));
ok(true, 'el filtro de vencidos deja 3');
await pagina.uncheck('input[name=vencidos]');

// Detalle: un reclamo abierto pasa a En curso
await pagina.fill('input[name=texto]', 'Sin servicio desde');
await pagina.click('button:has-text("Buscar")');
await pagina.waitForFunction(() => document.querySelectorAll('tbody tr.fila-link').length === 1);
await pagina.click('tbody tr.fila-link');
await pagina.waitForSelector('h2:has-text("Historial")');
ok((await pagina.textContent('.badge.grande')).includes('Abierto'), 'el detalle muestra el estado Abierto');
await pagina.click('button:has-text("Pasar a Rechazado")');
await pagina.waitForSelector('.tarjeta .error');
ok((await pagina.textContent('.tarjeta .error')).includes('motivo'), 'rechazar sin motivo muestra el error de la regla');
await pagina.fill('textarea', 'Tomado. Se revisa la línea con el área técnica.');
await pagina.click('button:has-text("Pasar a En curso")');
await pagina.waitForFunction(() => document.querySelector('.badge.grande')?.textContent?.includes('En curso'));
ok((await pagina.locator('.historial li').count()) === 2, 'el cambio quedó en el historial');
await pagina.screenshot({ path: CAPTURAS + '3-detalle.png', fullPage: true });

// Alta de un reclamo nuevo con búsqueda de cliente
await pagina.click('nav a:has-text("Nuevo")');
await pagina.fill('input[placeholder="Razón social o CUIT"]', 'farm');
await pagina.waitForSelector('.sugerencias button');
await pagina.click('.sugerencias button');
ok((await pagina.inputValue('input[formcontrolname=clienteCuit]')) === '30698765433', 'elegir el cliente completa el CUIT');
await pagina.fill('input[formcontrolname=clienteCuit]', '30698765434');
await pagina.locator('input[formcontrolname=clienteCuit]').blur();
ok((await pagina.locator('text=El CUIT no es válido').count()) === 1, 'el formulario valida el dígito verificador');
await pagina.fill('input[formcontrolname=clienteCuit]', '30698765433');
await pagina.fill('input[formcontrolname=asunto]', 'No imprime la factura electrónica');
await pagina.fill('textarea[formcontrolname=descripcion]', 'Desde la actualización de ayer el sistema no genera el PDF de la factura.');
await pagina.selectOption('select[formcontrolname=prioridad]', 'Alta');
await pagina.screenshot({ path: CAPTURAS + '4-nuevo.png' });
await pagina.click('button:has-text("Crear reclamo")');
await pagina.waitForURL(/reclamos\/R-\d{4}-\d{6}$/);
await pagina.waitForSelector('.badge.grande');
ok((await pagina.textContent('h1')).includes('No imprime la factura'), 'el reclamo nuevo se creó y abre su detalle');

// Supervisor: reporte de SLA
await pagina.click('button:has-text("Salir")');
await pagina.waitForURL('**/ingresar');
await llenar(pagina, 'input[formcontrolname=login]', 'supervisor');
await llenar(pagina, 'input[formcontrolname=clave]', 'demo1234');
await pagina.click('button:has-text("Ingresar")');
await pagina.waitForURL('**/reclamos');
await pagina.click('nav a:has-text("Reporte")');
await pagina.waitForSelector('tbody tr td .badge');
ok((await pagina.locator('tbody tr').count()) === 4, 'el reporte tiene una fila por prioridad');
await pagina.screenshot({ path: CAPTURAS + '5-reporte.png' });

// Celular
await pagina.setViewportSize({ width: 390, height: 844 });
await pagina.click('nav a:has-text("Reclamos")');
await pagina.waitForSelector('tbody tr.fila-link');
await pagina.screenshot({ path: CAPTURAS + '6-celular.png' });

ok(errores.length === 0, 'sin errores de JavaScript' + (errores.length ? ': ' + errores.join(' | ') : ''));
await navegador.close();
console.log('Recorrido completo.');
