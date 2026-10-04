-- Mesa de Reclamos: stored procedures y funciones.
-- Las escrituras son PROCEDURE (se llaman con CALL); las lecturas son FUNCTION que devuelven tablas.
-- Errores de negocio: SQLSTATE 'RC404' (no existe) y 'RC422' (regla no cumplida). La capa de datos
-- los traduce a NoEncontradoException y ReglaDeNegocioException.

CREATE OR REPLACE PROCEDURE sp_reclamo_crear(
    p_cuit          CHAR(11),
    p_asunto        VARCHAR(120),
    p_descripcion   TEXT,
    p_prioridad     VARCHAR(10),
    p_canal         VARCHAR(10),
    p_fecha_alta    TIMESTAMPTZ,
    p_fecha_limite  TIMESTAMPTZ,
    p_usuario       VARCHAR(40),
    INOUT p_numero  VARCHAR(20) DEFAULT NULL)
LANGUAGE plpgsql AS $$
DECLARE
    v_cliente_id BIGINT;
    v_reclamo_id BIGINT;
BEGIN
    SELECT id INTO v_cliente_id FROM clientes WHERE cuit = p_cuit;
    IF v_cliente_id IS NULL THEN
        RAISE EXCEPTION 'No hay un cliente con CUIT %.', p_cuit USING ERRCODE = 'RC404';
    END IF;

    p_numero := 'R-' || to_char(p_fecha_alta, 'YYYY') || '-' || lpad(nextval('reclamo_numero_seq')::text, 6, '0');

    INSERT INTO reclamos (numero, cliente_id, asunto, descripcion, prioridad, canal, fecha_alta, fecha_limite)
    VALUES (p_numero, v_cliente_id, p_asunto, p_descripcion, p_prioridad, p_canal, p_fecha_alta, p_fecha_limite)
    RETURNING id INTO v_reclamo_id;

    INSERT INTO reclamo_historial (reclamo_id, fecha, usuario, estado_anterior, estado_nuevo, comentario)
    VALUES (v_reclamo_id, p_fecha_alta, p_usuario, NULL, 'Abierto',
            'Reclamo creado por ' || CASE p_canal WHEN 'Web' THEN 'la web' WHEN 'Telefono' THEN 'teléfono'
                                                  WHEN 'Email' THEN 'email' ELSE 'integración SOAP' END);
END;
$$;

CREATE OR REPLACE PROCEDURE sp_reclamo_cambiar_estado(
    p_numero      VARCHAR(20),
    p_estado      VARCHAR(20),
    p_usuario     VARCHAR(40),
    p_comentario  TEXT)
LANGUAGE plpgsql AS $$
DECLARE
    v_id      BIGINT;
    v_actual  VARCHAR(20);
BEGIN
    -- FOR UPDATE bloquea la fila: si dos operadores cambian el mismo reclamo a la vez, el segundo espera
    -- y valida contra el estado que dejó el primero.
    SELECT id, estado INTO v_id, v_actual FROM reclamos WHERE numero = p_numero FOR UPDATE;
    IF v_id IS NULL THEN
        RAISE EXCEPTION 'No existe el reclamo %.', p_numero USING ERRCODE = 'RC404';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM estado_transiciones WHERE desde = v_actual AND hacia = p_estado) THEN
        RAISE EXCEPTION 'Un reclamo % no puede pasar a %.', v_actual, p_estado USING ERRCODE = 'RC422';
    END IF;

    UPDATE reclamos
       SET estado       = p_estado,
           asignado_a   = CASE WHEN p_estado = 'EnCurso' THEN COALESCE(asignado_a, p_usuario) ELSE asignado_a END,
           fecha_cierre = CASE WHEN p_estado IN ('Cerrado', 'Rechazado') THEN now() ELSE NULL END
     WHERE id = v_id;

    INSERT INTO reclamo_historial (reclamo_id, usuario, estado_anterior, estado_nuevo, comentario)
    VALUES (v_id, p_usuario, v_actual, p_estado, p_comentario);
END;
$$;

CREATE OR REPLACE PROCEDURE sp_reclamo_comentar(
    p_numero      VARCHAR(20),
    p_usuario     VARCHAR(40),
    p_comentario  TEXT)
LANGUAGE plpgsql AS $$
DECLARE
    v_id BIGINT;
BEGIN
    SELECT id INTO v_id FROM reclamos WHERE numero = p_numero;
    IF v_id IS NULL THEN
        RAISE EXCEPTION 'No existe el reclamo %.', p_numero USING ERRCODE = 'RC404';
    END IF;

    INSERT INTO reclamo_historial (reclamo_id, usuario, comentario) VALUES (v_id, p_usuario, p_comentario);
END;
$$;

-- Búsqueda paginada. total_filas viene en cada fila (COUNT(*) OVER ()) para no hacer dos consultas.
CREATE OR REPLACE FUNCTION fn_reclamos_buscar(
    p_estado         VARCHAR(20),
    p_prioridad      VARCHAR(10),
    p_texto          VARCHAR(100),
    p_solo_vencidos  BOOLEAN,
    p_desde_fila     INT,
    p_cantidad       INT)
RETURNS TABLE (
    id BIGINT, numero VARCHAR, cuit CHAR(11), razon_social VARCHAR, asunto VARCHAR, descripcion TEXT,
    prioridad VARCHAR, canal VARCHAR, estado VARCHAR, asignado_a VARCHAR,
    fecha_alta TIMESTAMPTZ, fecha_limite TIMESTAMPTZ, fecha_cierre TIMESTAMPTZ, total_filas BIGINT)
LANGUAGE sql STABLE AS $$
    SELECT r.id, r.numero, c.cuit, c.razon_social, r.asunto, r.descripcion,
           r.prioridad, r.canal, r.estado, r.asignado_a,
           r.fecha_alta, r.fecha_limite, r.fecha_cierre, COUNT(*) OVER ()
      FROM reclamos r
      JOIN clientes c ON c.id = r.cliente_id
     WHERE (p_estado IS NULL OR r.estado = p_estado)
       AND (p_prioridad IS NULL OR r.prioridad = p_prioridad)
       AND (p_texto IS NULL
            OR r.numero ILIKE '%' || p_texto || '%'
            OR r.asunto ILIKE '%' || p_texto || '%'
            OR c.razon_social ILIKE '%' || p_texto || '%'
            OR c.cuit LIKE '%' || p_texto || '%')
       AND (NOT p_solo_vencidos
            OR (r.estado NOT IN ('Resuelto', 'Cerrado', 'Rechazado') AND now() > r.fecha_limite))
     ORDER BY CASE r.prioridad WHEN 'Critica' THEN 1 WHEN 'Alta' THEN 2 WHEN 'Media' THEN 3 ELSE 4 END,
              r.fecha_limite, r.id
    OFFSET p_desde_fila LIMIT p_cantidad;
$$;

CREATE OR REPLACE FUNCTION fn_reclamo_obtener(p_numero VARCHAR(20))
RETURNS TABLE (
    id BIGINT, numero VARCHAR, cuit CHAR(11), razon_social VARCHAR, asunto VARCHAR, descripcion TEXT,
    prioridad VARCHAR, canal VARCHAR, estado VARCHAR, asignado_a VARCHAR,
    fecha_alta TIMESTAMPTZ, fecha_limite TIMESTAMPTZ, fecha_cierre TIMESTAMPTZ)
LANGUAGE sql STABLE AS $$
    SELECT r.id, r.numero, c.cuit, c.razon_social, r.asunto, r.descripcion,
           r.prioridad, r.canal, r.estado, r.asignado_a, r.fecha_alta, r.fecha_limite, r.fecha_cierre
      FROM reclamos r
      JOIN clientes c ON c.id = r.cliente_id
     WHERE r.numero = p_numero;
$$;

CREATE OR REPLACE FUNCTION fn_reclamo_historial(p_numero VARCHAR(20))
RETURNS TABLE (fecha TIMESTAMPTZ, usuario VARCHAR, estado_anterior VARCHAR, estado_nuevo VARCHAR, comentario TEXT)
LANGUAGE sql STABLE AS $$
    SELECT h.fecha, h.usuario, h.estado_anterior, h.estado_nuevo, h.comentario
      FROM reclamo_historial h
      JOIN reclamos r ON r.id = h.reclamo_id
     WHERE r.numero = p_numero
     ORDER BY h.fecha, h.id;
$$;

-- Cumplimiento del SLA por prioridad, para los reclamos dados de alta entre dos fechas.
-- Un reclamo "se resolvió" la primera vez que pasó a Resuelto (o al cerrarse, si nunca pasó por Resuelto).
-- Los rechazados no cuentan para el SLA.
CREATE OR REPLACE FUNCTION fn_reporte_sla(p_desde DATE, p_hasta DATE)
RETURNS TABLE (prioridad VARCHAR, total INT, resueltos INT, dentro_sla INT, vencidos INT, horas_promedio NUMERIC)
LANGUAGE sql STABLE AS $$
    WITH base AS (
        SELECT r.prioridad, r.estado, r.fecha_alta, r.fecha_limite,
               COALESCE((SELECT MIN(h.fecha) FROM reclamo_historial h
                          WHERE h.reclamo_id = r.id AND h.estado_nuevo = 'Resuelto'),
                        CASE WHEN r.estado = 'Cerrado' THEN r.fecha_cierre END) AS resuelto_en
          FROM reclamos r
         WHERE r.estado <> 'Rechazado'
           AND r.fecha_alta >= p_desde
           AND r.fecha_alta < p_hasta + 1
    )
    SELECT prioridad,
           COUNT(*)::int,
           COUNT(resuelto_en)::int,
           COUNT(*) FILTER (WHERE resuelto_en <= fecha_limite)::int,
           COUNT(*) FILTER (WHERE resuelto_en > fecha_limite
                               OR (resuelto_en IS NULL AND now() > fecha_limite))::int,
           ROUND(AVG(EXTRACT(EPOCH FROM resuelto_en - fecha_alta) / 3600)::numeric, 1)
      FROM base
     GROUP BY prioridad
     ORDER BY CASE prioridad WHEN 'Critica' THEN 1 WHEN 'Alta' THEN 2 WHEN 'Media' THEN 3 ELSE 4 END;
$$;
