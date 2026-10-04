-- Mesa de Reclamos: tablas. Se puede correr varias veces (IF NOT EXISTS).

CREATE TABLE IF NOT EXISTS clientes (
    id            BIGSERIAL PRIMARY KEY,
    cuit          CHAR(11)     NOT NULL UNIQUE,
    razon_social  VARCHAR(120) NOT NULL,
    email         VARCHAR(120) NOT NULL,
    telefono      VARCHAR(30)
);

CREATE TABLE IF NOT EXISTS usuarios (
    id          BIGSERIAL PRIMARY KEY,
    login       VARCHAR(40)  NOT NULL UNIQUE,
    nombre      VARCHAR(80)  NOT NULL,
    rol         VARCHAR(20)  NOT NULL CHECK (rol IN ('Operador', 'Supervisor')),
    hash_clave  TEXT         NOT NULL
);

-- Qué cambios de estado están permitidos. La misma tabla que Reclamo.Transiciones en C#.
CREATE TABLE IF NOT EXISTS estado_transiciones (
    desde  VARCHAR(20) NOT NULL,
    hacia  VARCHAR(20) NOT NULL,
    PRIMARY KEY (desde, hacia)
);

INSERT INTO estado_transiciones (desde, hacia) VALUES
    ('Abierto', 'EnCurso'), ('Abierto', 'Rechazado'),
    ('EnCurso', 'EsperandoCliente'), ('EnCurso', 'Resuelto'),
    ('EsperandoCliente', 'EnCurso'), ('EsperandoCliente', 'Cerrado'),
    ('Resuelto', 'Cerrado'), ('Resuelto', 'EnCurso')
ON CONFLICT DO NOTHING;

CREATE SEQUENCE IF NOT EXISTS reclamo_numero_seq;

CREATE TABLE IF NOT EXISTS reclamos (
    id            BIGSERIAL PRIMARY KEY,
    numero        VARCHAR(20)  NOT NULL UNIQUE,
    cliente_id    BIGINT       NOT NULL REFERENCES clientes(id),
    asunto        VARCHAR(120) NOT NULL,
    descripcion   TEXT         NOT NULL,
    prioridad     VARCHAR(10)  NOT NULL CHECK (prioridad IN ('Baja', 'Media', 'Alta', 'Critica')),
    canal         VARCHAR(10)  NOT NULL CHECK (canal IN ('Web', 'Telefono', 'Email', 'Soap')),
    estado        VARCHAR(20)  NOT NULL DEFAULT 'Abierto'
                  CHECK (estado IN ('Abierto', 'EnCurso', 'EsperandoCliente', 'Resuelto', 'Cerrado', 'Rechazado')),
    asignado_a    VARCHAR(40),
    fecha_alta    TIMESTAMPTZ  NOT NULL,
    fecha_limite  TIMESTAMPTZ  NOT NULL,
    fecha_cierre  TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_reclamos_estado ON reclamos (estado);
CREATE INDEX IF NOT EXISTS ix_reclamos_cliente ON reclamos (cliente_id);
CREATE INDEX IF NOT EXISTS ix_reclamos_alta ON reclamos (fecha_alta);

-- Cada cambio de estado y cada comentario queda registrado: es la auditoría del reclamo.
CREATE TABLE IF NOT EXISTS reclamo_historial (
    id               BIGSERIAL PRIMARY KEY,
    reclamo_id       BIGINT       NOT NULL REFERENCES reclamos(id),
    fecha            TIMESTAMPTZ  NOT NULL DEFAULT now(),
    usuario          VARCHAR(40)  NOT NULL,
    estado_anterior  VARCHAR(20),
    estado_nuevo     VARCHAR(20),
    comentario       TEXT
);

CREATE INDEX IF NOT EXISTS ix_historial_reclamo ON reclamo_historial (reclamo_id);
