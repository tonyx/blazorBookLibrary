-- migrate:up

-- ============================================================================
-- Stream: Author
-- ============================================================================

-- 1. Add Causality Columns & Indexes
ALTER TABLE public.events_01_Author
    ADD COLUMN IF NOT EXISTS correlation_id uuid DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS causation_id uuid DEFAULT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_Author_correlation_id 
    ON public.events_01_Author(correlation_id) 
    WHERE correlation_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_Author_causation_id 
    ON public.events_01_Author(causation_id) 
    WHERE causation_id IS NOT NULL;

-- 2. Update insert helper function with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Author_event_and_return_id(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
BEGIN
    INSERT INTO events_01_Author(event, aggregate_id, distance_from_latest_snapshot, timestamp, md, correlation_id, causation_id)
    VALUES(event_in, aggregate_id, distance_from_latest_snapshot, now(), md, correlation_id, causation_id)
    RETURNING id INTO inserted_id;
    RETURN inserted_id;
END;
$$;

-- 3. Update opt_lock procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Author_aggregate_event_and_return_id_opt_lock(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
    found_last_event_id integer;
BEGIN
    SELECT id INTO found_last_event_id 
    FROM events_01_Author 
    WHERE events_01_Author.aggregate_id = insert_md_01_Author_aggregate_event_and_return_id_opt_lock.aggregate_id 
    ORDER BY id DESC LIMIT 1;

    IF last_event_id = 0 THEN
        IF found_last_event_id IS NOT NULL THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected no previous events, but found event %', found_last_event_id;
        END IF;
    ELSIF last_event_id > 0 THEN
        IF found_last_event_id IS NULL OR found_last_event_id <> last_event_id THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected last event id %, but found %', last_event_id, found_last_event_id;
        END IF;
    END IF;

    event_id := insert_md_01_Author_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_Author(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 4. Update opt_lock2 procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Author_aggregate_event_and_return_id_opt_lock2(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN extra_stream_names text[],
    IN extra_event_ids int[],
    IN extra_aggregate_ids uuid[],
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
BEGIN
    -- Perform the main optimistic locking check for the aggregate itself
    PERFORM check_last_event_id_opt_lock('events_01_Author', aggregate_id, last_event_id);

    -- Perform the checks for extra constraints
    IF extra_stream_names IS NOT NULL THEN
        FOR i IN 1..cardinality(extra_stream_names) LOOP
            PERFORM check_last_event_id_opt_lock(extra_stream_names[i], extra_aggregate_ids[i], extra_event_ids[i]);
        END LOOP;
    END IF;

    event_id := insert_md_01_Author_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_Author(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 5. Add Atomic NoOp Patch & Snapshot Purge Procedure
CREATE OR REPLACE FUNCTION patch_event_noop_and_purge_snapshots_01_Author(
    IN target_aggregate_id uuid,
    IN target_event_id int,
    IN noop_payload text
)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE events_01_Author
    SET event = noop_payload
    WHERE aggregate_id = target_aggregate_id AND id = target_event_id;

    DELETE FROM snapshots_01_Author
    WHERE aggregate_id = target_aggregate_id AND (event_id >= target_event_id OR event_id IS NULL);
END;
$$;

-- ============================================================================
-- Stream: Book
-- ============================================================================

-- 1. Add Causality Columns & Indexes
ALTER TABLE public.events_01_Book
    ADD COLUMN IF NOT EXISTS correlation_id uuid DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS causation_id uuid DEFAULT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_Book_correlation_id 
    ON public.events_01_Book(correlation_id) 
    WHERE correlation_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_Book_causation_id 
    ON public.events_01_Book(causation_id) 
    WHERE causation_id IS NOT NULL;

-- 2. Update insert helper function with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Book_event_and_return_id(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
BEGIN
    INSERT INTO events_01_Book(event, aggregate_id, distance_from_latest_snapshot, timestamp, md, correlation_id, causation_id)
    VALUES(event_in, aggregate_id, distance_from_latest_snapshot, now(), md, correlation_id, causation_id)
    RETURNING id INTO inserted_id;
    RETURN inserted_id;
END;
$$;

-- 3. Update opt_lock procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Book_aggregate_event_and_return_id_opt_lock(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
    found_last_event_id integer;
BEGIN
    SELECT id INTO found_last_event_id 
    FROM events_01_Book 
    WHERE events_01_Book.aggregate_id = insert_md_01_Book_aggregate_event_and_return_id_opt_lock.aggregate_id 
    ORDER BY id DESC LIMIT 1;

    IF last_event_id = 0 THEN
        IF found_last_event_id IS NOT NULL THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected no previous events, but found event %', found_last_event_id;
        END IF;
    ELSIF last_event_id > 0 THEN
        IF found_last_event_id IS NULL OR found_last_event_id <> last_event_id THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected last event id %, but found %', last_event_id, found_last_event_id;
        END IF;
    END IF;

    event_id := insert_md_01_Book_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_Book(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 4. Update opt_lock2 procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Book_aggregate_event_and_return_id_opt_lock2(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN extra_stream_names text[],
    IN extra_event_ids int[],
    IN extra_aggregate_ids uuid[],
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
BEGIN
    -- Perform the main optimistic locking check for the aggregate itself
    PERFORM check_last_event_id_opt_lock('events_01_book', aggregate_id, last_event_id);

    -- Perform the checks for extra constraints
    IF extra_stream_names IS NOT NULL THEN
        FOR i IN 1..cardinality(extra_stream_names) LOOP
            PERFORM check_last_event_id_opt_lock(extra_stream_names[i], extra_aggregate_ids[i], extra_event_ids[i]);
        END LOOP;
    END IF;

    event_id := insert_md_01_Book_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_Book(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 5. Add Atomic NoOp Patch & Snapshot Purge Procedure
CREATE OR REPLACE FUNCTION patch_event_noop_and_purge_snapshots_01_Book(
    IN target_aggregate_id uuid,
    IN target_event_id int,
    IN noop_payload text
)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE events_01_Book
    SET event = noop_payload
    WHERE aggregate_id = target_aggregate_id AND id = target_event_id;

    DELETE FROM snapshots_01_Book
    WHERE aggregate_id = target_aggregate_id AND (event_id >= target_event_id OR event_id IS NULL);
END;
$$;

-- ============================================================================
-- Stream: DistributionPoint
-- ============================================================================

-- 1. Add Causality Columns & Indexes
ALTER TABLE public.events_01_DistributionPoint
    ADD COLUMN IF NOT EXISTS correlation_id uuid DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS causation_id uuid DEFAULT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_DistributionPoint_correlation_id 
    ON public.events_01_DistributionPoint(correlation_id) 
    WHERE correlation_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_DistributionPoint_causation_id 
    ON public.events_01_DistributionPoint(causation_id) 
    WHERE causation_id IS NOT NULL;

-- 2. Update insert helper function with causality support
CREATE OR REPLACE FUNCTION insert_md_01_DistributionPoint_event_and_return_id(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
BEGIN
    INSERT INTO events_01_DistributionPoint(event, aggregate_id, distance_from_latest_snapshot, timestamp, md, correlation_id, causation_id)
    VALUES(event_in, aggregate_id, distance_from_latest_snapshot, now(), md, correlation_id, causation_id)
    RETURNING id INTO inserted_id;
    RETURN inserted_id;
END;
$$;

-- 3. Update opt_lock procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_DistributionPoint_aggregate_event_and_return_id_opt_lock(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
    found_last_event_id integer;
BEGIN
    SELECT id INTO found_last_event_id 
    FROM events_01_DistributionPoint 
    WHERE events_01_DistributionPoint.aggregate_id = insert_md_01_DistributionPoint_aggregate_event_and_return_id_opt_lock.aggregate_id 
    ORDER BY id DESC LIMIT 1;

    IF last_event_id = 0 THEN
        IF found_last_event_id IS NOT NULL THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected no previous events, but found event %', found_last_event_id;
        END IF;
    ELSIF last_event_id > 0 THEN
        IF found_last_event_id IS NULL OR found_last_event_id <> last_event_id THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected last event id %, but found %', last_event_id, found_last_event_id;
        END IF;
    END IF;

    event_id := insert_md_01_DistributionPoint_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_DistributionPoint(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 4. Update opt_lock2 procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_DistributionPoint_aggregate_event_and_return_id_opt_lock2(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN extra_stream_names text[],
    IN extra_event_ids int[],
    IN extra_aggregate_ids uuid[],
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
BEGIN
    -- Perform the main optimistic locking check for the aggregate itself
    PERFORM check_last_event_id_opt_lock('events_01_distributionpoint', aggregate_id, last_event_id);

    -- Perform the checks for extra constraints
    IF extra_stream_names IS NOT NULL THEN
        FOR i IN 1..cardinality(extra_stream_names) LOOP
            PERFORM check_last_event_id_opt_lock(extra_stream_names[i], extra_aggregate_ids[i], extra_event_ids[i]);
        END LOOP;
    END IF;

    event_id := insert_md_01_DistributionPoint_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_DistributionPoint(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 5. Add Atomic NoOp Patch & Snapshot Purge Procedure
CREATE OR REPLACE FUNCTION patch_event_noop_and_purge_snapshots_01_DistributionPoint(
    IN target_aggregate_id uuid,
    IN target_event_id int,
    IN noop_payload text
)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE events_01_DistributionPoint
    SET event = noop_payload
    WHERE aggregate_id = target_aggregate_id AND id = target_event_id;

    DELETE FROM snapshots_01_DistributionPoint
    WHERE aggregate_id = target_aggregate_id AND (event_id >= target_event_id OR event_id IS NULL);
END;
$$;

-- Stream: Editor
-- ============================================================================

-- 1. Add Causality Columns & Indexes
ALTER TABLE public.events_01_Editor
    ADD COLUMN IF NOT EXISTS correlation_id uuid DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS causation_id uuid DEFAULT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_Editor_correlation_id 
    ON public.events_01_Editor(correlation_id) 
    WHERE correlation_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_Editor_causation_id 
    ON public.events_01_Editor(causation_id) 
    WHERE causation_id IS NOT NULL;

-- 2. Update insert helper function with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Editor_event_and_return_id(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
BEGIN
    INSERT INTO events_01_Editor(event, aggregate_id, distance_from_latest_snapshot, timestamp, md, correlation_id, causation_id)
    VALUES(event_in, aggregate_id, distance_from_latest_snapshot, now(), md, correlation_id, causation_id)
    RETURNING id INTO inserted_id;
    RETURN inserted_id;
END;
$$;

-- 3. Update opt_lock procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Editor_aggregate_event_and_return_id_opt_lock(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
    found_last_event_id integer;
BEGIN
    SELECT id INTO found_last_event_id 
    FROM events_01_Editor 
    WHERE events_01_Editor.aggregate_id = insert_md_01_Editor_aggregate_event_and_return_id_opt_lock.aggregate_id 
    ORDER BY id DESC LIMIT 1;

    IF last_event_id = 0 THEN
        IF found_last_event_id IS NOT NULL THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected no previous events, but found event %', found_last_event_id;
        END IF;
    ELSIF last_event_id > 0 THEN
        IF found_last_event_id IS NULL OR found_last_event_id <> last_event_id THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected last event id %, but found %', last_event_id, found_last_event_id;
        END IF;
    END IF;

    event_id := insert_md_01_Editor_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_Editor(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 4. Update opt_lock2 procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Editor_aggregate_event_and_return_id_opt_lock2(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN extra_stream_names text[],
    IN extra_event_ids int[],
    IN extra_aggregate_ids uuid[],
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
BEGIN
    -- Perform the main optimistic locking check for the aggregate itself
    PERFORM check_last_event_id_opt_lock('events_01_editor', aggregate_id, last_event_id);

    -- Perform the checks for extra constraints
    IF extra_stream_names IS NOT NULL THEN
        FOR i IN 1..cardinality(extra_stream_names) LOOP
            PERFORM check_last_event_id_opt_lock(extra_stream_names[i], extra_aggregate_ids[i], extra_event_ids[i]);
        END LOOP;
    END IF;

    event_id := insert_md_01_Editor_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_Editor(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 5. Add Atomic NoOp Patch & Snapshot Purge Procedure
CREATE OR REPLACE FUNCTION patch_event_noop_and_purge_snapshots_01_Editor(
    IN target_aggregate_id uuid,
    IN target_event_id int,
    IN noop_payload text
)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE events_01_Editor
    SET event = noop_payload
    WHERE aggregate_id = target_aggregate_id AND id = target_event_id;

    DELETE FROM snapshots_01_Editor
    WHERE aggregate_id = target_aggregate_id AND (event_id >= target_event_id OR event_id IS NULL);
END;
$$;

-- ============================================================================
-- Stream: IsbnRegistry
-- ============================================================================

-- 1. Add Causality Columns & Indexes
ALTER TABLE public.events_01_IsbnRegistry
    ADD COLUMN IF NOT EXISTS correlation_id uuid DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS causation_id uuid DEFAULT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_IsbnRegistry_correlation_id 
    ON public.events_01_IsbnRegistry(correlation_id) 
    WHERE correlation_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_IsbnRegistry_causation_id 
    ON public.events_01_IsbnRegistry(causation_id) 
    WHERE causation_id IS NOT NULL;

-- 2. Update insert helper function with causality support
CREATE OR REPLACE FUNCTION insert_md_01_IsbnRegistry_event_and_return_id(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
BEGIN
    INSERT INTO events_01_IsbnRegistry(event, aggregate_id, distance_from_latest_snapshot, timestamp, md, correlation_id, causation_id)
    VALUES(event_in, aggregate_id, distance_from_latest_snapshot, now(), md, correlation_id, causation_id)
    RETURNING id INTO inserted_id;
    RETURN inserted_id;
END;
$$;

-- 3. Update opt_lock procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_IsbnRegistry_aggregate_event_and_return_id_opt_lock(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
    found_last_event_id integer;
BEGIN
    SELECT id INTO found_last_event_id 
    FROM events_01_IsbnRegistry 
    WHERE events_01_IsbnRegistry.aggregate_id = insert_md_01_IsbnRegistry_aggregate_event_and_return_id_opt_lock.aggregate_id 
    ORDER BY id DESC LIMIT 1;

    IF last_event_id = 0 THEN
        IF found_last_event_id IS NOT NULL THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected no previous events, but found event %', found_last_event_id;
        END IF;
    ELSIF last_event_id > 0 THEN
        IF found_last_event_id IS NULL OR found_last_event_id <> last_event_id THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected last event id %, but found %', last_event_id, found_last_event_id;
        END IF;
    END IF;

    event_id := insert_md_01_IsbnRegistry_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_IsbnRegistry(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 4. Update opt_lock2 procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_IsbnRegistry_aggregate_event_and_return_id_opt_lock2(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN extra_stream_names text[],
    IN extra_event_ids int[],
    IN extra_aggregate_ids uuid[],
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
BEGIN
    -- Perform the main optimistic locking check for the aggregate itself
    PERFORM check_last_event_id_opt_lock('events_01_isbnregistry', aggregate_id, last_event_id);

    -- Perform the checks for extra constraints
    IF extra_stream_names IS NOT NULL THEN
        FOR i IN 1..cardinality(extra_stream_names) LOOP
            PERFORM check_last_event_id_opt_lock(extra_stream_names[i], extra_aggregate_ids[i], extra_event_ids[i]);
        END LOOP;
    END IF;

    event_id := insert_md_01_IsbnRegistry_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_IsbnRegistry(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 5. Add Atomic NoOp Patch & Snapshot Purge Procedure
CREATE OR REPLACE FUNCTION patch_event_noop_and_purge_snapshots_01_IsbnRegistry(
    IN target_aggregate_id uuid,
    IN target_event_id int,
    IN noop_payload text
)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE events_01_IsbnRegistry
    SET event = noop_payload
    WHERE aggregate_id = target_aggregate_id AND id = target_event_id;

    DELETE FROM snapshots_01_IsbnRegistry
    WHERE aggregate_id = target_aggregate_id AND (event_id >= target_event_id OR event_id IS NULL);
END;
$$;

-- ============================================================================
-- Stream: Loan
-- ============================================================================

-- 1. Add Causality Columns & Indexes
ALTER TABLE public.events_01_Loan
    ADD COLUMN IF NOT EXISTS correlation_id uuid DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS causation_id uuid DEFAULT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_Loan_correlation_id 
    ON public.events_01_Loan(correlation_id) 
    WHERE correlation_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_Loan_causation_id 
    ON public.events_01_Loan(causation_id) 
    WHERE causation_id IS NOT NULL;

-- 2. Update insert helper function with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Loan_event_and_return_id(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
BEGIN
    INSERT INTO events_01_Loan(event, aggregate_id, distance_from_latest_snapshot, timestamp, md, correlation_id, causation_id)
    VALUES(event_in, aggregate_id, distance_from_latest_snapshot, now(), md, correlation_id, causation_id)
    RETURNING id INTO inserted_id;
    RETURN inserted_id;
END;
$$;

-- 3. Update opt_lock procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Loan_aggregate_event_and_return_id_opt_lock(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
    found_last_event_id integer;
BEGIN
    SELECT id INTO found_last_event_id 
    FROM events_01_Loan 
    WHERE events_01_Loan.aggregate_id = insert_md_01_Loan_aggregate_event_and_return_id_opt_lock.aggregate_id 
    ORDER BY id DESC LIMIT 1;

    IF last_event_id = 0 THEN
        IF found_last_event_id IS NOT NULL THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected no previous events, but found event %', found_last_event_id;
        END IF;
    ELSIF last_event_id > 0 THEN
        IF found_last_event_id IS NULL OR found_last_event_id <> last_event_id THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected last event id %, but found %', last_event_id, found_last_event_id;
        END IF;
    END IF;

    event_id := insert_md_01_Loan_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_Loan(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 4. Update opt_lock2 procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Loan_aggregate_event_and_return_id_opt_lock2(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN extra_stream_names text[],
    IN extra_event_ids int[],
    IN extra_aggregate_ids uuid[],
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
BEGIN
    -- Perform the main optimistic locking check for the aggregate itself
    PERFORM check_last_event_id_opt_lock('events_01_loan', aggregate_id, last_event_id);

    -- Perform the checks for extra constraints
    IF extra_stream_names IS NOT NULL THEN
        FOR i IN 1..cardinality(extra_stream_names) LOOP
            PERFORM check_last_event_id_opt_lock(extra_stream_names[i], extra_aggregate_ids[i], extra_event_ids[i]);
        END LOOP;
    END IF;

    event_id := insert_md_01_Loan_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_Loan(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 5. Add Atomic NoOp Patch & Snapshot Purge Procedure
CREATE OR REPLACE FUNCTION patch_event_noop_and_purge_snapshots_01_Loan(
    IN target_aggregate_id uuid,
    IN target_event_id int,
    IN noop_payload text
)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE events_01_Loan
    SET event = noop_payload
    WHERE aggregate_id = target_aggregate_id AND id = target_event_id;

    DELETE FROM snapshots_01_Loan
    WHERE aggregate_id = target_aggregate_id AND (event_id >= target_event_id OR event_id IS NULL);
END;
$$;

-- ============================================================================
-- Stream: MailQueue
-- ============================================================================

-- 1. Add Causality Columns & Indexes
ALTER TABLE public.events_01_MailQueue
    ADD COLUMN IF NOT EXISTS correlation_id uuid DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS causation_id uuid DEFAULT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_MailQueue_correlation_id 
    ON public.events_01_MailQueue(correlation_id) 
    WHERE correlation_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_MailQueue_causation_id 
    ON public.events_01_MailQueue(causation_id) 
    WHERE causation_id IS NOT NULL;

-- 2. Update insert helper function with causality support
CREATE OR REPLACE FUNCTION insert_md_01_MailQueue_event_and_return_id(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
BEGIN
    INSERT INTO events_01_MailQueue(event, aggregate_id, distance_from_latest_snapshot, timestamp, md, correlation_id, causation_id)
    VALUES(event_in, aggregate_id, distance_from_latest_snapshot, now(), md, correlation_id, causation_id)
    RETURNING id INTO inserted_id;
    RETURN inserted_id;
END;
$$;

-- 3. Update opt_lock procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_MailQueue_aggregate_event_and_return_id_opt_lock(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
    found_last_event_id integer;
BEGIN
    SELECT id INTO found_last_event_id 
    FROM events_01_MailQueue 
    WHERE events_01_MailQueue.aggregate_id = insert_md_01_MailQueue_aggregate_event_and_return_id_opt_lock.aggregate_id 
    ORDER BY id DESC LIMIT 1;

    IF last_event_id = 0 THEN
        IF found_last_event_id IS NOT NULL THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected no previous events, but found event %', found_last_event_id;
        END IF;
    ELSIF last_event_id > 0 THEN
        IF found_last_event_id IS NULL OR found_last_event_id <> last_event_id THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected last event id %, but found %', last_event_id, found_last_event_id;
        END IF;
    END IF;

    event_id := insert_md_01_MailQueue_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_MailQueue(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 4. Update opt_lock2 procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_MailQueue_aggregate_event_and_return_id_opt_lock2(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN extra_stream_names text[],
    IN extra_event_ids int[],
    IN extra_aggregate_ids uuid[],
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
BEGIN
    -- Perform the main optimistic locking check for the aggregate itself
    PERFORM check_last_event_id_opt_lock('events_01_mailqueue', aggregate_id, last_event_id);

    -- Perform the checks for extra constraints
    IF extra_stream_names IS NOT NULL THEN
        FOR i IN 1..cardinality(extra_stream_names) LOOP
            PERFORM check_last_event_id_opt_lock(extra_stream_names[i], extra_aggregate_ids[i], extra_event_ids[i]);
        END LOOP;
    END IF;

    event_id := insert_md_01_MailQueue_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_MailQueue(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 5. Add Atomic NoOp Patch & Snapshot Purge Procedure
CREATE OR REPLACE FUNCTION patch_event_noop_and_purge_snapshots_01_MailQueue(
    IN target_aggregate_id uuid,
    IN target_event_id int,
    IN noop_payload text
)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE events_01_MailQueue
    SET event = noop_payload
    WHERE aggregate_id = target_aggregate_id AND id = target_event_id;

    DELETE FROM snapshots_01_MailQueue
    WHERE aggregate_id = target_aggregate_id AND (event_id >= target_event_id OR event_id IS NULL);
END;
$$;

-- Stream: Notification
-- ============================================================================

-- 1. Add Causality Columns & Indexes
ALTER TABLE public.events_01_Notification
    ADD COLUMN IF NOT EXISTS correlation_id uuid DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS causation_id uuid DEFAULT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_Notification_correlation_id 
    ON public.events_01_Notification(correlation_id) 
    WHERE correlation_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_Notification_causation_id 
    ON public.events_01_Notification(causation_id) 
    WHERE causation_id IS NOT NULL;

-- 2. Update insert helper function with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Notification_event_and_return_id(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
BEGIN
    INSERT INTO events_01_Notification(event, aggregate_id, distance_from_latest_snapshot, timestamp, md, correlation_id, causation_id)
    VALUES(event_in, aggregate_id, distance_from_latest_snapshot, now(), md, correlation_id, causation_id)
    RETURNING id INTO inserted_id;
    RETURN inserted_id;
END;
$$;

-- 3. Update opt_lock procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Notification_aggregate_event_and_return_id_opt_lock(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
    found_last_event_id integer;
BEGIN
    SELECT id INTO found_last_event_id 
    FROM events_01_Notification 
    WHERE events_01_Notification.aggregate_id = insert_md_01_Notification_aggregate_event_and_return_id_opt_lock.aggregate_id 
    ORDER BY id DESC LIMIT 1;

    IF last_event_id = 0 THEN
        IF found_last_event_id IS NOT NULL THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected no previous events, but found event %', found_last_event_id;
        END IF;
    ELSIF last_event_id > 0 THEN
        IF found_last_event_id IS NULL OR found_last_event_id <> last_event_id THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected last event id %, but found %', last_event_id, found_last_event_id;
        END IF;
    END IF;

    event_id := insert_md_01_Notification_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_Notification(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 4. Update opt_lock2 procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Notification_aggregate_event_and_return_id_opt_lock2(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN extra_stream_names text[],
    IN extra_event_ids int[],
    IN extra_aggregate_ids uuid[],
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
BEGIN
    -- Perform the main optimistic locking check for the aggregate itself
    PERFORM check_last_event_id_opt_lock('events_01_notification', aggregate_id, last_event_id);

    -- Perform the checks for extra constraints
    IF extra_stream_names IS NOT NULL THEN
        FOR i IN 1..cardinality(extra_stream_names) LOOP
            PERFORM check_last_event_id_opt_lock(extra_stream_names[i], extra_aggregate_ids[i], extra_event_ids[i]);
        END LOOP;
    END IF;

    event_id := insert_md_01_Notification_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_Notification(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 5. Add Atomic NoOp Patch & Snapshot Purge Procedure
CREATE OR REPLACE FUNCTION patch_event_noop_and_purge_snapshots_01_Notification(
    IN target_aggregate_id uuid,
    IN target_event_id int,
    IN noop_payload text
)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE events_01_Notification
    SET event = noop_payload
    WHERE aggregate_id = target_aggregate_id AND id = target_event_id;

    DELETE FROM snapshots_01_Notification
    WHERE aggregate_id = target_aggregate_id AND (event_id >= target_event_id OR event_id IS NULL);
END;
$$;

-- ============================================================================
-- Stream: Reservation
-- ============================================================================

-- 1. Add Causality Columns & Indexes
ALTER TABLE public.events_01_Reservation
    ADD COLUMN IF NOT EXISTS correlation_id uuid DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS causation_id uuid DEFAULT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_Reservation_correlation_id 
    ON public.events_01_Reservation(correlation_id) 
    WHERE correlation_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_Reservation_causation_id 
    ON public.events_01_Reservation(causation_id) 
    WHERE causation_id IS NOT NULL;

-- 2. Update insert helper function with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Reservation_event_and_return_id(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
BEGIN
    INSERT INTO events_01_Reservation(event, aggregate_id, distance_from_latest_snapshot, timestamp, md, correlation_id, causation_id)
    VALUES(event_in, aggregate_id, distance_from_latest_snapshot, now(), md, correlation_id, causation_id)
    RETURNING id INTO inserted_id;
    RETURN inserted_id;
END;
$$;

-- 3. Update opt_lock procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Reservation_aggregate_event_and_return_id_opt_lock(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
    found_last_event_id integer;
BEGIN
    SELECT id INTO found_last_event_id 
    FROM events_01_Reservation 
    WHERE events_01_Reservation.aggregate_id = insert_md_01_Reservation_aggregate_event_and_return_id_opt_lock.aggregate_id 
    ORDER BY id DESC LIMIT 1;

    IF last_event_id = 0 THEN
        IF found_last_event_id IS NOT NULL THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected no previous events, but found event %', found_last_event_id;
        END IF;
    ELSIF last_event_id > 0 THEN
        IF found_last_event_id IS NULL OR found_last_event_id <> last_event_id THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected last event id %, but found %', last_event_id, found_last_event_id;
        END IF;
    END IF;

    event_id := insert_md_01_Reservation_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_Reservation(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 4. Update opt_lock2 procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Reservation_aggregate_event_and_return_id_opt_lock2(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN extra_stream_names text[],
    IN extra_event_ids int[],
    IN extra_aggregate_ids uuid[],
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
BEGIN
    -- Perform the main optimistic locking check for the aggregate itself
    PERFORM check_last_event_id_opt_lock('events_01_reservation', aggregate_id, last_event_id);

    -- Perform the checks for extra constraints
    IF extra_stream_names IS NOT NULL THEN
        FOR i IN 1..cardinality(extra_stream_names) LOOP
            PERFORM check_last_event_id_opt_lock(extra_stream_names[i], extra_aggregate_ids[i], extra_event_ids[i]);
        END LOOP;
    END IF;

    event_id := insert_md_01_Reservation_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_Reservation(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 5. Add Atomic NoOp Patch & Snapshot Purge Procedure
CREATE OR REPLACE FUNCTION patch_event_noop_and_purge_snapshots_01_Reservation(
    IN target_aggregate_id uuid,
    IN target_event_id int,
    IN noop_payload text
)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE events_01_Reservation
    SET event = noop_payload
    WHERE aggregate_id = target_aggregate_id AND id = target_event_id;

    DELETE FROM snapshots_01_Reservation
    WHERE aggregate_id = target_aggregate_id AND (event_id >= target_event_id OR event_id IS NULL);
END;
$$;

-- ============================================================================
-- Stream: Review
-- ============================================================================

-- 1. Add Causality Columns & Indexes
ALTER TABLE public.events_01_Review
    ADD COLUMN IF NOT EXISTS correlation_id uuid DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS causation_id uuid DEFAULT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_Review_correlation_id 
    ON public.events_01_Review(correlation_id) 
    WHERE correlation_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_Review_causation_id 
    ON public.events_01_Review(causation_id) 
    WHERE causation_id IS NOT NULL;

-- 2. Update insert helper function with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Review_event_and_return_id(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
BEGIN
    INSERT INTO events_01_Review(event, aggregate_id, distance_from_latest_snapshot, timestamp, md, correlation_id, causation_id)
    VALUES(event_in, aggregate_id, distance_from_latest_snapshot, now(), md, correlation_id, causation_id)
    RETURNING id INTO inserted_id;
    RETURN inserted_id;
END;
$$;

-- 3. Update opt_lock procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Review_aggregate_event_and_return_id_opt_lock(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
    found_last_event_id integer;
BEGIN
    SELECT id INTO found_last_event_id 
    FROM events_01_Review 
    WHERE events_01_Review.aggregate_id = insert_md_01_Review_aggregate_event_and_return_id_opt_lock.aggregate_id 
    ORDER BY id DESC LIMIT 1;

    IF last_event_id = 0 THEN
        IF found_last_event_id IS NOT NULL THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected no previous events, but found event %', found_last_event_id;
        END IF;
    ELSIF last_event_id > 0 THEN
        IF found_last_event_id IS NULL OR found_last_event_id <> last_event_id THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected last event id %, but found %', last_event_id, found_last_event_id;
        END IF;
    END IF;

    event_id := insert_md_01_Review_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_Review(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 4. Update opt_lock2 procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Review_aggregate_event_and_return_id_opt_lock2(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN extra_stream_names text[],
    IN extra_event_ids int[],
    IN extra_aggregate_ids uuid[],
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
BEGIN
    -- Perform the main optimistic locking check for the aggregate itself
    PERFORM check_last_event_id_opt_lock('events_01_review', aggregate_id, last_event_id);

    -- Perform the checks for extra constraints
    IF extra_stream_names IS NOT NULL THEN
        FOR i IN 1..cardinality(extra_stream_names) LOOP
            PERFORM check_last_event_id_opt_lock(extra_stream_names[i], extra_aggregate_ids[i], extra_event_ids[i]);
        END LOOP;
    END IF;

    event_id := insert_md_01_Review_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_Review(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 5. Add Atomic NoOp Patch & Snapshot Purge Procedure
CREATE OR REPLACE FUNCTION patch_event_noop_and_purge_snapshots_01_Review(
    IN target_aggregate_id uuid,
    IN target_event_id int,
    IN noop_payload text
)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE events_01_Review
    SET event = noop_payload
    WHERE aggregate_id = target_aggregate_id AND id = target_event_id;

    DELETE FROM snapshots_01_Review
    WHERE aggregate_id = target_aggregate_id AND (event_id >= target_event_id OR event_id IS NULL);
END;
$$;

-- Stream: Tags
-- ============================================================================

-- 1. Add Causality Columns & Indexes
ALTER TABLE public.events_01_Tags
    ADD COLUMN IF NOT EXISTS correlation_id uuid DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS causation_id uuid DEFAULT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_Tags_correlation_id 
    ON public.events_01_Tags(correlation_id) 
    WHERE correlation_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_Tags_causation_id 
    ON public.events_01_Tags(causation_id) 
    WHERE causation_id IS NOT NULL;

-- 2. Update insert helper function with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Tags_event_and_return_id(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
BEGIN
    INSERT INTO events_01_Tags(event, aggregate_id, distance_from_latest_snapshot, timestamp, md, correlation_id, causation_id)
    VALUES(event_in, aggregate_id, distance_from_latest_snapshot, now(), md, correlation_id, causation_id)
    RETURNING id INTO inserted_id;
    RETURN inserted_id;
END;
$$;

-- 3. Update opt_lock procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Tags_aggregate_event_and_return_id_opt_lock(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
    found_last_event_id integer;
BEGIN
    SELECT id INTO found_last_event_id 
    FROM events_01_Tags 
    WHERE events_01_Tags.aggregate_id = insert_md_01_Tags_aggregate_event_and_return_id_opt_lock.aggregate_id 
    ORDER BY id DESC LIMIT 1;

    IF last_event_id = 0 THEN
        IF found_last_event_id IS NOT NULL THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected no previous events, but found event %', found_last_event_id;
        END IF;
    ELSIF last_event_id > 0 THEN
        IF found_last_event_id IS NULL OR found_last_event_id <> last_event_id THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected last event id %, but found %', last_event_id, found_last_event_id;
        END IF;
    END IF;

    event_id := insert_md_01_Tags_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_Tags(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 4. Update opt_lock2 procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Tags_aggregate_event_and_return_id_opt_lock2(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN extra_stream_names text[],
    IN extra_event_ids int[],
    IN extra_aggregate_ids uuid[],
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
BEGIN
    -- Perform the main optimistic locking check for the aggregate itself
    PERFORM check_last_event_id_opt_lock('events_01_tags', aggregate_id, last_event_id);

    -- Perform the checks for extra constraints
    IF extra_stream_names IS NOT NULL THEN
        FOR i IN 1..cardinality(extra_stream_names) LOOP
            PERFORM check_last_event_id_opt_lock(extra_stream_names[i], extra_aggregate_ids[i], extra_event_ids[i]);
        END LOOP;
    END IF;

    event_id := insert_md_01_Tags_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_Tags(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 5. Add Atomic NoOp Patch & Snapshot Purge Procedure
CREATE OR REPLACE FUNCTION patch_event_noop_and_purge_snapshots_01_Tags(
    IN target_aggregate_id uuid,
    IN target_event_id int,
    IN noop_payload text
)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE events_01_Tags
    SET event = noop_payload
    WHERE aggregate_id = target_aggregate_id AND id = target_event_id;

    DELETE FROM snapshots_01_Tags
    WHERE aggregate_id = target_aggregate_id AND (event_id >= target_event_id OR event_id IS NULL);
END;
$$;

-- ============================================================================
-- Stream: Tenant
-- ============================================================================

-- 1. Add Causality Columns & Indexes
ALTER TABLE public.events_01_Tenant
    ADD COLUMN IF NOT EXISTS correlation_id uuid DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS causation_id uuid DEFAULT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_Tenant_correlation_id 
    ON public.events_01_Tenant(correlation_id) 
    WHERE correlation_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_Tenant_causation_id 
    ON public.events_01_Tenant(causation_id) 
    WHERE causation_id IS NOT NULL;

-- 2. Update insert helper function with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Tenant_event_and_return_id(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
BEGIN
    INSERT INTO events_01_Tenant(event, aggregate_id, distance_from_latest_snapshot, timestamp, md, correlation_id, causation_id)
    VALUES(event_in, aggregate_id, distance_from_latest_snapshot, now(), md, correlation_id, causation_id)
    RETURNING id INTO inserted_id;
    RETURN inserted_id;
END;
$$;

-- 3. Update opt_lock procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Tenant_aggregate_event_and_return_id_opt_lock(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
    found_last_event_id integer;
BEGIN
    SELECT id INTO found_last_event_id 
    FROM events_01_Tenant 
    WHERE events_01_Tenant.aggregate_id = insert_md_01_Tenant_aggregate_event_and_return_id_opt_lock.aggregate_id 
    ORDER BY id DESC LIMIT 1;

    IF last_event_id = 0 THEN
        IF found_last_event_id IS NOT NULL THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected no previous events, but found event %', found_last_event_id;
        END IF;
    ELSIF last_event_id > 0 THEN
        IF found_last_event_id IS NULL OR found_last_event_id <> last_event_id THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected last event id %, but found %', last_event_id, found_last_event_id;
        END IF;
    END IF;

    event_id := insert_md_01_Tenant_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_Tenant(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 4. Update opt_lock2 procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_Tenant_aggregate_event_and_return_id_opt_lock2(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN extra_stream_names text[],
    IN extra_event_ids int[],
    IN extra_aggregate_ids uuid[],
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
BEGIN
    -- Perform the main optimistic locking check for the aggregate itself
    PERFORM check_last_event_id_opt_lock('events_01_tenant', aggregate_id, last_event_id);

    -- Perform the checks for extra constraints
    IF extra_stream_names IS NOT NULL THEN
        FOR i IN 1..cardinality(extra_stream_names) LOOP
            PERFORM check_last_event_id_opt_lock(extra_stream_names[i], extra_aggregate_ids[i], extra_event_ids[i]);
        END LOOP;
    END IF;

    event_id := insert_md_01_Tenant_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_Tenant(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 5. Add Atomic NoOp Patch & Snapshot Purge Procedure
CREATE OR REPLACE FUNCTION patch_event_noop_and_purge_snapshots_01_Tenant(
    IN target_aggregate_id uuid,
    IN target_event_id int,
    IN noop_payload text
)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE events_01_Tenant
    SET event = noop_payload
    WHERE aggregate_id = target_aggregate_id AND id = target_event_id;

    DELETE FROM snapshots_01_Tenant
    WHERE aggregate_id = target_aggregate_id AND (event_id >= target_event_id OR event_id IS NULL);
END;
$$;

-- ============================================================================
-- Stream: User
-- ============================================================================

-- 1. Add Causality Columns & Indexes
ALTER TABLE public.events_01_User
    ADD COLUMN IF NOT EXISTS correlation_id uuid DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS causation_id uuid DEFAULT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_User_correlation_id 
    ON public.events_01_User(correlation_id) 
    WHERE correlation_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_01_events_User_causation_id 
    ON public.events_01_User(causation_id) 
    WHERE causation_id IS NOT NULL;

-- 2. Update insert helper function with causality support
CREATE OR REPLACE FUNCTION insert_md_01_User_event_and_return_id(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
BEGIN
    INSERT INTO events_01_User(event, aggregate_id, distance_from_latest_snapshot, timestamp, md, correlation_id, causation_id)
    VALUES(event_in, aggregate_id, distance_from_latest_snapshot, now(), md, correlation_id, causation_id)
    RETURNING id INTO inserted_id;
    RETURN inserted_id;
END;
$$;

-- 3. Update opt_lock procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_User_aggregate_event_and_return_id_opt_lock(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
    found_last_event_id integer;
BEGIN
    SELECT id INTO found_last_event_id 
    FROM events_01_User 
    WHERE events_01_User.aggregate_id = insert_md_01_User_aggregate_event_and_return_id_opt_lock.aggregate_id 
    ORDER BY id DESC LIMIT 1;

    IF last_event_id = 0 THEN
        IF found_last_event_id IS NOT NULL THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected no previous events, but found event %', found_last_event_id;
        END IF;
    ELSIF last_event_id > 0 THEN
        IF found_last_event_id IS NULL OR found_last_event_id <> last_event_id THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected last event id %, but found %', last_event_id, found_last_event_id;
        END IF;
    END IF;

    event_id := insert_md_01_User_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_User(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 4. Update opt_lock2 procedure with causality support
CREATE OR REPLACE FUNCTION insert_md_01_User_aggregate_event_and_return_id_opt_lock2(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN extra_stream_names text[],
    IN extra_event_ids int[],
    IN extra_aggregate_ids uuid[],
    IN correlation_id uuid DEFAULT NULL,
    IN causation_id uuid DEFAULT NULL
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
BEGIN
    -- Perform the main optimistic locking check for the aggregate itself
    PERFORM check_last_event_id_opt_lock('events_01_user', aggregate_id, last_event_id);

    -- Perform the checks for extra constraints
    IF extra_stream_names IS NOT NULL THEN
        FOR i IN 1..cardinality(extra_stream_names) LOOP
            PERFORM check_last_event_id_opt_lock(extra_stream_names[i], extra_aggregate_ids[i], extra_event_ids[i]);
        END LOOP;
    END IF;

    event_id := insert_md_01_User_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md, correlation_id, causation_id);

    INSERT INTO aggregate_events_01_User(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 5. Add Atomic NoOp Patch & Snapshot Purge Procedure
CREATE OR REPLACE FUNCTION patch_event_noop_and_purge_snapshots_01_User(
    IN target_aggregate_id uuid,
    IN target_event_id int,
    IN noop_payload text
)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE events_01_User
    SET event = noop_payload
    WHERE aggregate_id = target_aggregate_id AND id = target_event_id;

    DELETE FROM snapshots_01_User
    WHERE aggregate_id = target_aggregate_id AND (event_id >= target_event_id OR event_id IS NULL);
END;
$$;

-- migrate:down

