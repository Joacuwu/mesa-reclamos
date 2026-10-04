-- Datos de ejemplo para la demo. Solo se cargan si la tabla de reclamos está vacía.
-- (Los usuarios se crean desde C# porque la contraseña se hashea con PBKDF2.)

INSERT INTO clientes (cuit, razon_social, email, telefono) VALUES
    ('30500011129', 'Distribuidora Norte S.A.',    'compras@distnorte.example',   '011 4300-1000'),
    ('30712345671', 'Logística del Plata S.R.L.',  'admin@logplata.example',      '011 4555-2020'),
    ('30698765433', 'Farmacias Unidas S.A.',       'sistemas@farmunidas.example', '0221 422-3300'),
    ('30700555662', 'Seguros Atlántico S.A.',      'operaciones@satlantico.example', '011 5199-4400'),
    ('20123456786', 'Martín Gómez',                'martin.gomez@correo.example', '11 3456-7890'),
    ('27334455667', 'Laura Fernández',             'laura.f@correo.example',      '11 2233-4455'),
    ('20387654322', 'Diego Ramírez',               'diego.ramirez@correo.example', NULL),
    ('27411223340', 'Sofía Herrera',               'sofia.herrera@correo.example', '11 6677-8899')
ON CONFLICT (cuit) DO NOTHING;

DO $$
DECLARE
    d         RECORD;
    v_numero  VARCHAR(20);
    v_alta    TIMESTAMPTZ;
    v_horas   INT;
    v_camino  TEXT[];
    v_paso    TEXT;
    i         INT;
BEGIN
    IF EXISTS (SELECT 1 FROM reclamos) THEN
        RETURN;
    END IF;

    FOR d IN SELECT * FROM (VALUES
        -- cuit, asunto, descripción, prioridad, canal, hace cuántas horas entró, estado final, horas hasta resolver
        ('30500011129', 'Facturación duplicada de setiembre', 'Nos llegaron dos facturas por el mismo servicio.', 'Alta', 'Email', 30, 'Cerrado', 10),
        ('30712345671', 'Caída del portal de clientes', 'No pueden ingresar al portal desde las 8 de la mañana.', 'Critica', 'Telefono', 6, 'EnCurso', NULL),
        ('30698765433', 'Error al descargar comprobantes', 'El PDF del comprobante sale en blanco.', 'Media', 'Web', 70, 'Resuelto', 20),
        ('30700555662', 'Cambio de domicilio de facturación', 'Pedimos actualizar el domicilio fiscal.', 'Baja', 'Soap', 20, 'Abierto', NULL),
        ('20123456786', 'No llegó el técnico', 'Tenía visita técnica agendada para ayer y no vino nadie.', 'Alta', 'Telefono', 40, 'EsperandoCliente', NULL),
        ('27334455667', 'Cobro de un servicio no contratado', 'Aparece un cargo por un servicio que no pedí.', 'Media', 'Web', 100, 'Cerrado', 60),
        ('20387654322', 'Consulta por plan corporativo', 'Quiero saber si puedo pasar a un plan corporativo.', 'Baja', 'Email', 10, 'Rechazado', NULL),
        ('27411223340', 'Sin servicio desde el lunes', 'No tengo servicio desde el lunes a la tarde.', 'Critica', 'Telefono', 3, 'Abierto', NULL),
        ('30500011129', 'Demora en la entrega de equipos', 'Los equipos que compramos no llegaron en fecha.', 'Alta', 'Web', 12, 'EnCurso', NULL),
        ('30712345671', 'Alta de usuarios nuevos', 'Necesitamos dar de alta 5 usuarios en el portal.', 'Media', 'Soap', 52, 'Abierto', NULL),
        ('30698765433', 'Lentitud en el sistema de turnos', 'El sistema tarda más de un minuto en responder.', 'Alta', 'Email', 80, 'Resuelto', 30),
        ('27334455667', 'Pedido de baja del servicio', 'Quiero dar de baja el servicio a fin de mes.', 'Baja', 'Web', 26, 'Resuelto', 5)
    ) AS t(cuit, asunto, descripcion, prioridad, canal, hace, estado_final, horas_resolver)
    LOOP
        v_alta := date_trunc('minute', now()) - make_interval(hours => d.hace);
        v_horas := CASE d.prioridad WHEN 'Critica' THEN 4 WHEN 'Alta' THEN 24 WHEN 'Media' THEN 48 ELSE 72 END;
        CALL sp_reclamo_crear(d.cuit, d.asunto, d.descripcion, d.prioridad, d.canal,
                              v_alta, v_alta + make_interval(hours => v_horas), 'operador', v_numero);

        v_camino := CASE d.estado_final
            WHEN 'EnCurso'          THEN ARRAY['EnCurso']
            WHEN 'EsperandoCliente' THEN ARRAY['EnCurso', 'EsperandoCliente']
            WHEN 'Resuelto'         THEN ARRAY['EnCurso', 'Resuelto']
            WHEN 'Cerrado'          THEN ARRAY['EnCurso', 'Resuelto', 'Cerrado']
            WHEN 'Rechazado'        THEN ARRAY['Rechazado']
            ELSE ARRAY[]::TEXT[] END;

        i := 0;
        FOREACH v_paso IN ARRAY v_camino LOOP
            i := i + 1;
            CALL sp_reclamo_cambiar_estado(v_numero, v_paso, 'operador',
                CASE v_paso WHEN 'Rechazado' THEN 'Es una consulta comercial: se derivó a ventas.'
                            WHEN 'Resuelto' THEN 'Se corrigió y se avisó al cliente.' ELSE NULL END);
        END LOOP;

        -- Las fechas del historial se acomodan para que la demo tenga tiempos realistas.
        UPDATE reclamo_historial h
           SET fecha = v_alta + CASE
                   WHEN h.estado_nuevo = 'EnCurso' THEN interval '30 minutes'
                   WHEN h.estado_nuevo IN ('Resuelto', 'Rechazado') THEN make_interval(hours => COALESCE(d.horas_resolver, 1))
                   WHEN h.estado_nuevo = 'Cerrado' THEN make_interval(hours => COALESCE(d.horas_resolver, 1) + 2)
                   WHEN h.estado_nuevo = 'EsperandoCliente' THEN interval '3 hours'
                   ELSE interval '0' END
          FROM reclamos r
         WHERE r.id = h.reclamo_id AND r.numero = v_numero;

        UPDATE reclamos r
           SET fecha_cierre = (SELECT MAX(h.fecha) FROM reclamo_historial h WHERE h.reclamo_id = r.id)
         WHERE r.numero = v_numero AND r.fecha_cierre IS NOT NULL;
    END LOOP;
END;
$$;
