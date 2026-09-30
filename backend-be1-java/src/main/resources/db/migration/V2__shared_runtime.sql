CREATE TABLE messaging.idempotency_records (
    scope varchar(150) NOT NULL,
    idempotency_key varchar(150) NOT NULL,
    request_hash char(64) NOT NULL,
    status varchar(20) NOT NULL DEFAULT 'PROCESSING'
        CONSTRAINT ck_idempotency_records_status CHECK (status IN ('PROCESSING', 'COMPLETED')),
    response_status integer,
    response_body jsonb,
    created_at timestamptz NOT NULL DEFAULT now(),
    completed_at timestamptz,
    expires_at timestamptz NOT NULL,
    CONSTRAINT pk_idempotency_records PRIMARY KEY (scope, idempotency_key),
    CONSTRAINT ck_idempotency_records_expiration CHECK (expires_at > created_at),
    CONSTRAINT ck_idempotency_records_completion CHECK (
        (status = 'PROCESSING' AND response_status IS NULL AND response_body IS NULL AND completed_at IS NULL)
        OR (status = 'COMPLETED' AND response_status BETWEEN 100 AND 599 AND completed_at IS NOT NULL))
);
CREATE INDEX ix_idempotency_records_expiration ON messaging.idempotency_records(expires_at);

REVOKE ALL ON messaging.idempotency_records FROM PUBLIC;
