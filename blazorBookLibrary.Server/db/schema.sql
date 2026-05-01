\restrict yH1FmOtuoQDBCmNhhzK7dVJqhIf5VU5ime7wieIMbrVpcy3HNCJ4PDzYu7tZvMV

-- Dumped from database version 17.9 (Homebrew)
-- Dumped by pg_dump version 18.0

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: insert_01_author_event_and_return_id(text, uuid); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_01_author_event_and_return_id(event_in text, aggregate_id uuid) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
BEGIN
INSERT INTO events_01_Author(event, aggregate_id, timestamp)
VALUES(event_in::text, aggregate_id,  now()) RETURNING id INTO inserted_id;
return inserted_id;
END;
$$;


--
-- Name: insert_01_book_aggregate_event_and_return_id(text, uuid); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_01_book_aggregate_event_and_return_id(event_in text, aggregate_id uuid) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
    event_id integer;
BEGIN
    event_id := insert_01_Book_event_and_return_id(event_in, aggregate_id);

INSERT INTO aggregate_events_01_Book(aggregate_id, event_id)
VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
return event_id;
END;
$$;


--
-- Name: insert_01_book_event_and_return_id(text, uuid); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_01_book_event_and_return_id(event_in text, aggregate_id uuid) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
BEGIN
INSERT INTO events_01_Book(event, aggregate_id, timestamp)
VALUES(event_in::text, aggregate_id,  now()) RETURNING id INTO inserted_id;
return inserted_id;
END;
$$;


--
-- Name: insert_01_editor_event_and_return_id(text, uuid); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_01_editor_event_and_return_id(event_in text, aggregate_id uuid) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
BEGIN
INSERT INTO events_01_Editor(event, aggregate_id, timestamp)
VALUES(event_in::text, aggregate_id,  now()) RETURNING id INTO inserted_id;
return inserted_id;
END;
$$;


--
-- Name: insert_01_isbnregistry_event_and_return_id(text, uuid); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_01_isbnregistry_event_and_return_id(event_in text, aggregate_id uuid) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
BEGIN
INSERT INTO events_01_IsbnRegistry(event, aggregate_id, timestamp)
VALUES(event_in::text, aggregate_id,  now()) RETURNING id INTO inserted_id;
return inserted_id;
END;
$$;


--
-- Name: insert_01_loan_event_and_return_id(text, uuid); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_01_loan_event_and_return_id(event_in text, aggregate_id uuid) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
BEGIN
INSERT INTO events_01_Loan(event, aggregate_id, timestamp)
VALUES(event_in::text, aggregate_id,  now()) RETURNING id INTO inserted_id;
return inserted_id;
END;
$$;


--
-- Name: insert_01_mailqueue_event_and_return_id(text, uuid); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_01_mailqueue_event_and_return_id(event_in text, aggregate_id uuid) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
BEGIN
INSERT INTO events_01_MailQueue(event, aggregate_id, timestamp)
VALUES(event_in::text, aggregate_id,  now()) RETURNING id INTO inserted_id;
return inserted_id;
END;
$$;


--
-- Name: insert_01_reservation_event_and_return_id(text, uuid); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_01_reservation_event_and_return_id(event_in text, aggregate_id uuid) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
BEGIN
INSERT INTO events_01_Reservation(event, aggregate_id, timestamp)
VALUES(event_in::text, aggregate_id,  now()) RETURNING id INTO inserted_id;
return inserted_id;
END;
$$;


--
-- Name: insert_01_review_event_and_return_id(text, uuid); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_01_review_event_and_return_id(event_in text, aggregate_id uuid) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
BEGIN
INSERT INTO events_01_Review(event, aggregate_id, timestamp)
VALUES(event_in::text, aggregate_id,  now()) RETURNING id INTO inserted_id;
return inserted_id;
END;
$$;


--
-- Name: insert_01_user_event_and_return_id(text, uuid); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_01_user_event_and_return_id(event_in text, aggregate_id uuid) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
BEGIN
INSERT INTO events_01_User(event, aggregate_id, timestamp)
VALUES(event_in::text, aggregate_id,  now()) RETURNING id INTO inserted_id;
return inserted_id;
END;
$$;


--
-- Name: insert_md_01_author_aggregate_event_and_return_id(text, uuid, integer, text); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_md_01_author_aggregate_event_and_return_id(event_in text, aggregate_id uuid, distance_from_latest_snapshot integer, md text) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
    event_id integer;
BEGIN
    event_id := insert_md_01_Author_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md);

INSERT INTO aggregate_events_01_Author(aggregate_id, event_id)
VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
return event_id;
END;
$$;


--
-- Name: insert_md_01_author_event_and_return_id(text, uuid, integer, text); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_md_01_author_event_and_return_id(event_in text, aggregate_id uuid, distance_from_latest_snapshot integer, md text) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
BEGIN
INSERT INTO events_01_Author(event, aggregate_id, distance_from_latest_snapshot, timestamp, md)
VALUES(event_in::text, aggregate_id, distance_from_latest_snapshot, now(), md) RETURNING id INTO inserted_id;
return inserted_id;
END;
$$;


--
-- Name: insert_md_01_book_aggregate_event_and_return_id(text, uuid, integer, text); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_md_01_book_aggregate_event_and_return_id(event_in text, aggregate_id uuid, distance_from_latest_snapshot integer, md text) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
    event_id integer;
BEGIN
    event_id := insert_md_01_Book_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md);

INSERT INTO aggregate_events_01_Book(aggregate_id, event_id)
VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
return event_id;
END;
$$;


--
-- Name: insert_md_01_book_event_and_return_id(text, uuid, integer, text); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_md_01_book_event_and_return_id(event_in text, aggregate_id uuid, distance_from_latest_snapshot integer, md text) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
BEGIN
INSERT INTO events_01_Book(event, aggregate_id, distance_from_latest_snapshot, timestamp, md)
VALUES(event_in::text, aggregate_id, distance_from_latest_snapshot, now(), md) RETURNING id INTO inserted_id;
return inserted_id;
END;
$$;


--
-- Name: insert_md_01_editor_aggregate_event_and_return_id(text, uuid, integer, text); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_md_01_editor_aggregate_event_and_return_id(event_in text, aggregate_id uuid, distance_from_latest_snapshot integer, md text) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
    event_id integer;
BEGIN
    event_id := insert_md_01_Editor_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md);

INSERT INTO aggregate_events_01_Editor(aggregate_id, event_id)
VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
return event_id;
END;
$$;


--
-- Name: insert_md_01_editor_event_and_return_id(text, uuid, integer, text); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_md_01_editor_event_and_return_id(event_in text, aggregate_id uuid, distance_from_latest_snapshot integer, md text) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
BEGIN
INSERT INTO events_01_Editor(event, aggregate_id, distance_from_latest_snapshot, timestamp, md)
VALUES(event_in::text, aggregate_id, distance_from_latest_snapshot, now(), md) RETURNING id INTO inserted_id;
return inserted_id;
END;
$$;


--
-- Name: insert_md_01_isbnregistry_aggregate_event_and_return_id(text, uuid, integer, text); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_md_01_isbnregistry_aggregate_event_and_return_id(event_in text, aggregate_id uuid, distance_from_latest_snapshot integer, md text) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
    event_id integer;
BEGIN
    event_id := insert_md_01_IsbnRegistry_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md);

INSERT INTO aggregate_events_01_IsbnRegistry(aggregate_id, event_id)
VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
return event_id;
END;
$$;


--
-- Name: insert_md_01_isbnregistry_event_and_return_id(text, uuid, integer, text); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_md_01_isbnregistry_event_and_return_id(event_in text, aggregate_id uuid, distance_from_latest_snapshot integer, md text) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
BEGIN
INSERT INTO events_01_IsbnRegistry(event, aggregate_id, distance_from_latest_snapshot, timestamp, md)
VALUES(event_in::text, aggregate_id, distance_from_latest_snapshot, now(), md) RETURNING id INTO inserted_id;
return inserted_id;
END;
$$;


--
-- Name: insert_md_01_loan_aggregate_event_and_return_id(text, uuid, integer, text); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_md_01_loan_aggregate_event_and_return_id(event_in text, aggregate_id uuid, distance_from_latest_snapshot integer, md text) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
    event_id integer;
BEGIN
    event_id := insert_md_01_Loan_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md);

INSERT INTO aggregate_events_01_Loan(aggregate_id, event_id)
VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
return event_id;
END;
$$;


--
-- Name: insert_md_01_loan_event_and_return_id(text, uuid, integer, text); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_md_01_loan_event_and_return_id(event_in text, aggregate_id uuid, distance_from_latest_snapshot integer, md text) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
BEGIN
INSERT INTO events_01_Loan(event, aggregate_id, distance_from_latest_snapshot, timestamp, md)
VALUES(event_in::text, aggregate_id, distance_from_latest_snapshot, now(), md) RETURNING id INTO inserted_id;
return inserted_id;
END;
$$;


--
-- Name: insert_md_01_mailqueue_aggregate_event_and_return_id(text, uuid, integer, text); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_md_01_mailqueue_aggregate_event_and_return_id(event_in text, aggregate_id uuid, distance_from_latest_snapshot integer, md text) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
    event_id integer;
BEGIN
    event_id := insert_md_01_MailQueue_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md);

INSERT INTO aggregate_events_01_MailQueue(aggregate_id, event_id)
VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
return event_id;
END;
$$;


--
-- Name: insert_md_01_mailqueue_event_and_return_id(text, uuid, integer, text); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_md_01_mailqueue_event_and_return_id(event_in text, aggregate_id uuid, distance_from_latest_snapshot integer, md text) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
BEGIN
INSERT INTO events_01_MailQueue(event, aggregate_id, distance_from_latest_snapshot, timestamp, md)
VALUES(event_in::text, aggregate_id, distance_from_latest_snapshot, now(), md) RETURNING id INTO inserted_id;
return inserted_id;
END;
$$;


--
-- Name: insert_md_01_reservation_aggregate_event_and_return_id(text, uuid, integer, text); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_md_01_reservation_aggregate_event_and_return_id(event_in text, aggregate_id uuid, distance_from_latest_snapshot integer, md text) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
    event_id integer;
BEGIN
    event_id := insert_md_01_Reservation_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md);

INSERT INTO aggregate_events_01_Reservation(aggregate_id, event_id)
VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
return event_id;
END;
$$;


--
-- Name: insert_md_01_reservation_event_and_return_id(text, uuid, integer, text); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_md_01_reservation_event_and_return_id(event_in text, aggregate_id uuid, distance_from_latest_snapshot integer, md text) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
BEGIN
INSERT INTO events_01_Reservation(event, aggregate_id, distance_from_latest_snapshot, timestamp, md)
VALUES(event_in::text, aggregate_id, distance_from_latest_snapshot, now(), md) RETURNING id INTO inserted_id;
return inserted_id;
END;
$$;


--
-- Name: insert_md_01_review_aggregate_event_and_return_id(text, uuid, integer, text); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_md_01_review_aggregate_event_and_return_id(event_in text, aggregate_id uuid, distance_from_latest_snapshot integer, md text) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
    event_id integer;
BEGIN
    event_id := insert_md_01_Review_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md);

INSERT INTO aggregate_events_01_Review(aggregate_id, event_id)
VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
return event_id;
END;
$$;


--
-- Name: insert_md_01_review_event_and_return_id(text, uuid, integer, text); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_md_01_review_event_and_return_id(event_in text, aggregate_id uuid, distance_from_latest_snapshot integer, md text) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
BEGIN
INSERT INTO events_01_Review(event, aggregate_id, distance_from_latest_snapshot, timestamp, md)
VALUES(event_in::text, aggregate_id, distance_from_latest_snapshot, now(), md) RETURNING id INTO inserted_id;
return inserted_id;
END;
$$;


--
-- Name: insert_md_01_user_aggregate_event_and_return_id(text, uuid, integer, text); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_md_01_user_aggregate_event_and_return_id(event_in text, aggregate_id uuid, distance_from_latest_snapshot integer, md text) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
    event_id integer;
BEGIN
    event_id := insert_md_01_User_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md);

INSERT INTO aggregate_events_01_User(aggregate_id, event_id)
VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
return event_id;
END;
$$;


--
-- Name: insert_md_01_user_event_and_return_id(text, uuid, integer, text); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.insert_md_01_user_event_and_return_id(event_in text, aggregate_id uuid, distance_from_latest_snapshot integer, md text) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
inserted_id integer;
BEGIN
INSERT INTO events_01_User(event, aggregate_id, distance_from_latest_snapshot, timestamp, md)
VALUES(event_in::text, aggregate_id, distance_from_latest_snapshot, now(), md) RETURNING id INTO inserted_id;
return inserted_id;
END;
$$;


--
-- Name: aggregate_events_01_author_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aggregate_events_01_author_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: aggregate_events_01_author; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aggregate_events_01_author (
    id integer DEFAULT nextval('public.aggregate_events_01_author_id_seq'::regclass) NOT NULL,
    aggregate_id uuid NOT NULL,
    event_id integer
);


--
-- Name: aggregate_events_01_book_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aggregate_events_01_book_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aggregate_events_01_book; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aggregate_events_01_book (
    id integer DEFAULT nextval('public.aggregate_events_01_book_id_seq'::regclass) NOT NULL,
    aggregate_id uuid NOT NULL,
    event_id integer
);


--
-- Name: aggregate_events_01_editor_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aggregate_events_01_editor_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aggregate_events_01_editor; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aggregate_events_01_editor (
    id integer DEFAULT nextval('public.aggregate_events_01_editor_id_seq'::regclass) NOT NULL,
    aggregate_id uuid NOT NULL,
    event_id integer
);


--
-- Name: aggregate_events_01_isbnregistry_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aggregate_events_01_isbnregistry_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aggregate_events_01_isbnregistry; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aggregate_events_01_isbnregistry (
    id integer DEFAULT nextval('public.aggregate_events_01_isbnregistry_id_seq'::regclass) NOT NULL,
    aggregate_id uuid NOT NULL,
    event_id integer
);


--
-- Name: aggregate_events_01_loan_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aggregate_events_01_loan_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aggregate_events_01_loan; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aggregate_events_01_loan (
    id integer DEFAULT nextval('public.aggregate_events_01_loan_id_seq'::regclass) NOT NULL,
    aggregate_id uuid NOT NULL,
    event_id integer
);


--
-- Name: aggregate_events_01_mailqueue_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aggregate_events_01_mailqueue_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aggregate_events_01_mailqueue; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aggregate_events_01_mailqueue (
    id integer DEFAULT nextval('public.aggregate_events_01_mailqueue_id_seq'::regclass) NOT NULL,
    aggregate_id uuid NOT NULL,
    event_id integer
);


--
-- Name: aggregate_events_01_reservation_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aggregate_events_01_reservation_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aggregate_events_01_reservation; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aggregate_events_01_reservation (
    id integer DEFAULT nextval('public.aggregate_events_01_reservation_id_seq'::regclass) NOT NULL,
    aggregate_id uuid NOT NULL,
    event_id integer
);


--
-- Name: aggregate_events_01_review_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aggregate_events_01_review_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aggregate_events_01_review; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aggregate_events_01_review (
    id integer DEFAULT nextval('public.aggregate_events_01_review_id_seq'::regclass) NOT NULL,
    aggregate_id uuid NOT NULL,
    event_id integer
);


--
-- Name: aggregate_events_01_user_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aggregate_events_01_user_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aggregate_events_01_user; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aggregate_events_01_user (
    id integer DEFAULT nextval('public.aggregate_events_01_user_id_seq'::regclass) NOT NULL,
    aggregate_id uuid NOT NULL,
    event_id integer
);


--
-- Name: events_01_author; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.events_01_author (
    id integer NOT NULL,
    aggregate_id uuid NOT NULL,
    event text NOT NULL,
    published boolean DEFAULT false NOT NULL,
    "timestamp" timestamp without time zone NOT NULL,
    distance_from_latest_snapshot integer,
    md text
);


--
-- Name: events_01_author_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.events_01_author ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.events_01_author_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: events_01_book; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.events_01_book (
    id integer NOT NULL,
    aggregate_id uuid NOT NULL,
    event text NOT NULL,
    published boolean DEFAULT false NOT NULL,
    "timestamp" timestamp without time zone NOT NULL,
    distance_from_latest_snapshot integer,
    md text
);


--
-- Name: events_01_book_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.events_01_book ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.events_01_book_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: events_01_editor; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.events_01_editor (
    id integer NOT NULL,
    aggregate_id uuid NOT NULL,
    event text NOT NULL,
    published boolean DEFAULT false NOT NULL,
    "timestamp" timestamp without time zone NOT NULL,
    distance_from_latest_snapshot integer,
    md text
);


--
-- Name: events_01_editor_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.events_01_editor ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.events_01_editor_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: events_01_isbnregistry; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.events_01_isbnregistry (
    id integer NOT NULL,
    aggregate_id uuid NOT NULL,
    event text NOT NULL,
    published boolean DEFAULT false NOT NULL,
    "timestamp" timestamp without time zone NOT NULL,
    distance_from_latest_snapshot integer,
    md text
);


--
-- Name: events_01_isbnregistry_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.events_01_isbnregistry ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.events_01_isbnregistry_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: events_01_loan; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.events_01_loan (
    id integer NOT NULL,
    aggregate_id uuid NOT NULL,
    event text NOT NULL,
    published boolean DEFAULT false NOT NULL,
    "timestamp" timestamp without time zone NOT NULL,
    distance_from_latest_snapshot integer,
    md text
);


--
-- Name: events_01_loan_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.events_01_loan ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.events_01_loan_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: events_01_mailqueue; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.events_01_mailqueue (
    id integer NOT NULL,
    aggregate_id uuid NOT NULL,
    event text NOT NULL,
    published boolean DEFAULT false NOT NULL,
    "timestamp" timestamp without time zone NOT NULL,
    distance_from_latest_snapshot integer,
    md text
);


--
-- Name: events_01_mailqueue_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.events_01_mailqueue ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.events_01_mailqueue_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: events_01_reservation; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.events_01_reservation (
    id integer NOT NULL,
    aggregate_id uuid NOT NULL,
    event text NOT NULL,
    published boolean DEFAULT false NOT NULL,
    "timestamp" timestamp without time zone NOT NULL,
    distance_from_latest_snapshot integer,
    md text
);


--
-- Name: events_01_reservation_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.events_01_reservation ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.events_01_reservation_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: events_01_review; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.events_01_review (
    id integer NOT NULL,
    aggregate_id uuid NOT NULL,
    event text NOT NULL,
    published boolean DEFAULT false NOT NULL,
    "timestamp" timestamp without time zone NOT NULL,
    distance_from_latest_snapshot integer,
    md text
);


--
-- Name: events_01_review_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.events_01_review ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.events_01_review_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: events_01_user; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.events_01_user (
    id integer NOT NULL,
    aggregate_id uuid NOT NULL,
    event text NOT NULL,
    published boolean DEFAULT false NOT NULL,
    "timestamp" timestamp without time zone NOT NULL,
    distance_from_latest_snapshot integer,
    md text
);


--
-- Name: events_01_user_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.events_01_user ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.events_01_user_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: schema_migrations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.schema_migrations (
    version character varying(128) NOT NULL
);


--
-- Name: snapshots_01_author_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.snapshots_01_author_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: snapshots_01_author; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.snapshots_01_author (
    id integer DEFAULT nextval('public.snapshots_01_author_id_seq'::regclass) NOT NULL,
    snapshot text NOT NULL,
    event_id integer,
    aggregate_id uuid NOT NULL,
    "timestamp" timestamp without time zone NOT NULL,
    is_deleted boolean DEFAULT false NOT NULL
);


--
-- Name: snapshots_01_book_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.snapshots_01_book_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: snapshots_01_book; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.snapshots_01_book (
    id integer DEFAULT nextval('public.snapshots_01_book_id_seq'::regclass) NOT NULL,
    snapshot text NOT NULL,
    event_id integer,
    aggregate_id uuid NOT NULL,
    "timestamp" timestamp without time zone NOT NULL,
    is_deleted boolean DEFAULT false NOT NULL
);


--
-- Name: snapshots_01_editor_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.snapshots_01_editor_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: snapshots_01_editor; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.snapshots_01_editor (
    id integer DEFAULT nextval('public.snapshots_01_editor_id_seq'::regclass) NOT NULL,
    snapshot text NOT NULL,
    event_id integer,
    aggregate_id uuid NOT NULL,
    "timestamp" timestamp without time zone NOT NULL,
    is_deleted boolean DEFAULT false NOT NULL
);


--
-- Name: snapshots_01_isbnregistry_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.snapshots_01_isbnregistry_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: snapshots_01_isbnregistry; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.snapshots_01_isbnregistry (
    id integer DEFAULT nextval('public.snapshots_01_isbnregistry_id_seq'::regclass) NOT NULL,
    snapshot text NOT NULL,
    event_id integer,
    aggregate_id uuid NOT NULL,
    "timestamp" timestamp without time zone NOT NULL,
    is_deleted boolean DEFAULT false NOT NULL
);


--
-- Name: snapshots_01_loan_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.snapshots_01_loan_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: snapshots_01_loan; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.snapshots_01_loan (
    id integer DEFAULT nextval('public.snapshots_01_loan_id_seq'::regclass) NOT NULL,
    snapshot text NOT NULL,
    event_id integer,
    aggregate_id uuid NOT NULL,
    "timestamp" timestamp without time zone NOT NULL,
    is_deleted boolean DEFAULT false NOT NULL
);


--
-- Name: snapshots_01_mailqueue_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.snapshots_01_mailqueue_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: snapshots_01_mailqueue; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.snapshots_01_mailqueue (
    id integer DEFAULT nextval('public.snapshots_01_mailqueue_id_seq'::regclass) NOT NULL,
    snapshot text NOT NULL,
    event_id integer,
    aggregate_id uuid NOT NULL,
    "timestamp" timestamp without time zone NOT NULL,
    is_deleted boolean DEFAULT false NOT NULL
);


--
-- Name: snapshots_01_reservation_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.snapshots_01_reservation_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: snapshots_01_reservation; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.snapshots_01_reservation (
    id integer DEFAULT nextval('public.snapshots_01_reservation_id_seq'::regclass) NOT NULL,
    snapshot text NOT NULL,
    event_id integer,
    aggregate_id uuid NOT NULL,
    "timestamp" timestamp without time zone NOT NULL,
    is_deleted boolean DEFAULT false NOT NULL
);


--
-- Name: snapshots_01_review_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.snapshots_01_review_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: snapshots_01_review; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.snapshots_01_review (
    id integer DEFAULT nextval('public.snapshots_01_review_id_seq'::regclass) NOT NULL,
    snapshot text NOT NULL,
    event_id integer,
    aggregate_id uuid NOT NULL,
    "timestamp" timestamp without time zone NOT NULL,
    is_deleted boolean DEFAULT false NOT NULL
);


--
-- Name: snapshots_01_user_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.snapshots_01_user_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: snapshots_01_user; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.snapshots_01_user (
    id integer DEFAULT nextval('public.snapshots_01_user_id_seq'::regclass) NOT NULL,
    snapshot text NOT NULL,
    event_id integer,
    aggregate_id uuid NOT NULL,
    "timestamp" timestamp without time zone NOT NULL,
    is_deleted boolean DEFAULT false NOT NULL
);


--
-- Name: aggregate_events_01_author aggregate_events_01_author_event_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_author
    ADD CONSTRAINT aggregate_events_01_author_event_id_key UNIQUE (event_id);


--
-- Name: aggregate_events_01_author aggregate_events_01_author_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_author
    ADD CONSTRAINT aggregate_events_01_author_pkey PRIMARY KEY (id);


--
-- Name: aggregate_events_01_book aggregate_events_01_book_event_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_book
    ADD CONSTRAINT aggregate_events_01_book_event_id_key UNIQUE (event_id);


--
-- Name: aggregate_events_01_book aggregate_events_01_book_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_book
    ADD CONSTRAINT aggregate_events_01_book_pkey PRIMARY KEY (id);


--
-- Name: aggregate_events_01_editor aggregate_events_01_editor_event_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_editor
    ADD CONSTRAINT aggregate_events_01_editor_event_id_key UNIQUE (event_id);


--
-- Name: aggregate_events_01_editor aggregate_events_01_editor_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_editor
    ADD CONSTRAINT aggregate_events_01_editor_pkey PRIMARY KEY (id);


--
-- Name: aggregate_events_01_isbnregistry aggregate_events_01_isbnregistry_event_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_isbnregistry
    ADD CONSTRAINT aggregate_events_01_isbnregistry_event_id_key UNIQUE (event_id);


--
-- Name: aggregate_events_01_isbnregistry aggregate_events_01_isbnregistry_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_isbnregistry
    ADD CONSTRAINT aggregate_events_01_isbnregistry_pkey PRIMARY KEY (id);


--
-- Name: aggregate_events_01_loan aggregate_events_01_loan_event_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_loan
    ADD CONSTRAINT aggregate_events_01_loan_event_id_key UNIQUE (event_id);


--
-- Name: aggregate_events_01_loan aggregate_events_01_loan_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_loan
    ADD CONSTRAINT aggregate_events_01_loan_pkey PRIMARY KEY (id);


--
-- Name: aggregate_events_01_mailqueue aggregate_events_01_mailqueue_event_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_mailqueue
    ADD CONSTRAINT aggregate_events_01_mailqueue_event_id_key UNIQUE (event_id);


--
-- Name: aggregate_events_01_mailqueue aggregate_events_01_mailqueue_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_mailqueue
    ADD CONSTRAINT aggregate_events_01_mailqueue_pkey PRIMARY KEY (id);


--
-- Name: aggregate_events_01_reservation aggregate_events_01_reservation_event_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_reservation
    ADD CONSTRAINT aggregate_events_01_reservation_event_id_key UNIQUE (event_id);


--
-- Name: aggregate_events_01_reservation aggregate_events_01_reservation_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_reservation
    ADD CONSTRAINT aggregate_events_01_reservation_pkey PRIMARY KEY (id);


--
-- Name: aggregate_events_01_review aggregate_events_01_review_event_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_review
    ADD CONSTRAINT aggregate_events_01_review_event_id_key UNIQUE (event_id);


--
-- Name: aggregate_events_01_review aggregate_events_01_review_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_review
    ADD CONSTRAINT aggregate_events_01_review_pkey PRIMARY KEY (id);


--
-- Name: aggregate_events_01_user aggregate_events_01_user_event_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_user
    ADD CONSTRAINT aggregate_events_01_user_event_id_key UNIQUE (event_id);


--
-- Name: aggregate_events_01_user aggregate_events_01_user_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_user
    ADD CONSTRAINT aggregate_events_01_user_pkey PRIMARY KEY (id);


--
-- Name: events_01_author events_author_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.events_01_author
    ADD CONSTRAINT events_author_pkey PRIMARY KEY (id);


--
-- Name: events_01_book events_book_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.events_01_book
    ADD CONSTRAINT events_book_pkey PRIMARY KEY (id);


--
-- Name: events_01_editor events_editor_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.events_01_editor
    ADD CONSTRAINT events_editor_pkey PRIMARY KEY (id);


--
-- Name: events_01_isbnregistry events_isbnregistry_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.events_01_isbnregistry
    ADD CONSTRAINT events_isbnregistry_pkey PRIMARY KEY (id);


--
-- Name: events_01_loan events_loan_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.events_01_loan
    ADD CONSTRAINT events_loan_pkey PRIMARY KEY (id);


--
-- Name: events_01_mailqueue events_mailqueue_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.events_01_mailqueue
    ADD CONSTRAINT events_mailqueue_pkey PRIMARY KEY (id);


--
-- Name: events_01_reservation events_reservation_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.events_01_reservation
    ADD CONSTRAINT events_reservation_pkey PRIMARY KEY (id);


--
-- Name: events_01_review events_review_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.events_01_review
    ADD CONSTRAINT events_review_pkey PRIMARY KEY (id);


--
-- Name: events_01_user events_user_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.events_01_user
    ADD CONSTRAINT events_user_pkey PRIMARY KEY (id);


--
-- Name: schema_migrations schema_migrations_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.schema_migrations
    ADD CONSTRAINT schema_migrations_pkey PRIMARY KEY (version);


--
-- Name: snapshots_01_author snapshots_author_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.snapshots_01_author
    ADD CONSTRAINT snapshots_author_pkey PRIMARY KEY (id);


--
-- Name: snapshots_01_book snapshots_book_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.snapshots_01_book
    ADD CONSTRAINT snapshots_book_pkey PRIMARY KEY (id);


--
-- Name: snapshots_01_editor snapshots_editor_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.snapshots_01_editor
    ADD CONSTRAINT snapshots_editor_pkey PRIMARY KEY (id);


--
-- Name: snapshots_01_isbnregistry snapshots_isbnregistry_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.snapshots_01_isbnregistry
    ADD CONSTRAINT snapshots_isbnregistry_pkey PRIMARY KEY (id);


--
-- Name: snapshots_01_loan snapshots_loan_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.snapshots_01_loan
    ADD CONSTRAINT snapshots_loan_pkey PRIMARY KEY (id);


--
-- Name: snapshots_01_mailqueue snapshots_mailqueue_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.snapshots_01_mailqueue
    ADD CONSTRAINT snapshots_mailqueue_pkey PRIMARY KEY (id);


--
-- Name: snapshots_01_reservation snapshots_reservation_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.snapshots_01_reservation
    ADD CONSTRAINT snapshots_reservation_pkey PRIMARY KEY (id);


--
-- Name: snapshots_01_review snapshots_review_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.snapshots_01_review
    ADD CONSTRAINT snapshots_review_pkey PRIMARY KEY (id);


--
-- Name: snapshots_01_user snapshots_user_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.snapshots_01_user
    ADD CONSTRAINT snapshots_user_pkey PRIMARY KEY (id);


--
-- Name: ix_01_aggregate_events_author_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_aggregate_events_author_id ON public.aggregate_events_01_author USING btree (aggregate_id);


--
-- Name: ix_01_aggregate_events_book_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_aggregate_events_book_id ON public.aggregate_events_01_book USING btree (aggregate_id);


--
-- Name: ix_01_aggregate_events_editor_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_aggregate_events_editor_id ON public.aggregate_events_01_editor USING btree (aggregate_id);


--
-- Name: ix_01_aggregate_events_isbnregistry_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_aggregate_events_isbnregistry_id ON public.aggregate_events_01_isbnregistry USING btree (aggregate_id);


--
-- Name: ix_01_aggregate_events_loan_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_aggregate_events_loan_id ON public.aggregate_events_01_loan USING btree (aggregate_id);


--
-- Name: ix_01_aggregate_events_mailqueue_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_aggregate_events_mailqueue_id ON public.aggregate_events_01_mailqueue USING btree (aggregate_id);


--
-- Name: ix_01_aggregate_events_reservation_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_aggregate_events_reservation_id ON public.aggregate_events_01_reservation USING btree (aggregate_id);


--
-- Name: ix_01_aggregate_events_review_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_aggregate_events_review_id ON public.aggregate_events_01_review USING btree (aggregate_id);


--
-- Name: ix_01_aggregate_events_user_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_aggregate_events_user_id ON public.aggregate_events_01_user USING btree (aggregate_id);


--
-- Name: ix_01_events_author_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_events_author_id ON public.events_01_author USING btree (aggregate_id);


--
-- Name: ix_01_events_author_timestamp; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_events_author_timestamp ON public.events_01_author USING btree ("timestamp");


--
-- Name: ix_01_events_book_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_events_book_id ON public.events_01_book USING btree (aggregate_id);


--
-- Name: ix_01_events_book_timestamp; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_events_book_timestamp ON public.events_01_book USING btree ("timestamp");


--
-- Name: ix_01_events_editor_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_events_editor_id ON public.events_01_editor USING btree (aggregate_id);


--
-- Name: ix_01_events_editor_timestamp; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_events_editor_timestamp ON public.events_01_editor USING btree ("timestamp");


--
-- Name: ix_01_events_isbnregistry_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_events_isbnregistry_id ON public.events_01_isbnregistry USING btree (aggregate_id);


--
-- Name: ix_01_events_isbnregistry_timestamp; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_events_isbnregistry_timestamp ON public.events_01_isbnregistry USING btree ("timestamp");


--
-- Name: ix_01_events_loan_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_events_loan_id ON public.events_01_loan USING btree (aggregate_id);


--
-- Name: ix_01_events_loan_timestamp; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_events_loan_timestamp ON public.events_01_loan USING btree ("timestamp");


--
-- Name: ix_01_events_mailqueue_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_events_mailqueue_id ON public.events_01_mailqueue USING btree (aggregate_id);


--
-- Name: ix_01_events_mailqueue_timestamp; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_events_mailqueue_timestamp ON public.events_01_mailqueue USING btree ("timestamp");


--
-- Name: ix_01_events_reservation_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_events_reservation_id ON public.events_01_reservation USING btree (aggregate_id);


--
-- Name: ix_01_events_reservation_timestamp; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_events_reservation_timestamp ON public.events_01_reservation USING btree ("timestamp");


--
-- Name: ix_01_events_review_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_events_review_id ON public.events_01_review USING btree (aggregate_id);


--
-- Name: ix_01_events_review_timestamp; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_events_review_timestamp ON public.events_01_review USING btree ("timestamp");


--
-- Name: ix_01_events_user_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_events_user_id ON public.events_01_user USING btree (aggregate_id);


--
-- Name: ix_01_events_user_timestamp; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_events_user_timestamp ON public.events_01_user USING btree ("timestamp");


--
-- Name: ix_01_snapshot_author_aggregate_id_and_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_author_aggregate_id_and_id ON public.snapshots_01_author USING btree (aggregate_id, id DESC);


--
-- Name: ix_01_snapshot_author_event_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_author_event_id ON public.snapshots_01_author USING btree (event_id);


--
-- Name: ix_01_snapshot_author_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_author_id ON public.snapshots_01_author USING btree (aggregate_id);


--
-- Name: ix_01_snapshot_book_aggregate_id_and_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_book_aggregate_id_and_id ON public.snapshots_01_book USING btree (aggregate_id, id DESC);


--
-- Name: ix_01_snapshot_book_event_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_book_event_id ON public.snapshots_01_book USING btree (event_id);


--
-- Name: ix_01_snapshot_book_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_book_id ON public.snapshots_01_book USING btree (aggregate_id);


--
-- Name: ix_01_snapshot_editor_aggregate_id_and_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_editor_aggregate_id_and_id ON public.snapshots_01_editor USING btree (aggregate_id, id DESC);


--
-- Name: ix_01_snapshot_editor_event_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_editor_event_id ON public.snapshots_01_editor USING btree (event_id);


--
-- Name: ix_01_snapshot_editor_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_editor_id ON public.snapshots_01_editor USING btree (aggregate_id);


--
-- Name: ix_01_snapshot_isbnregistry_aggregate_id_and_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_isbnregistry_aggregate_id_and_id ON public.snapshots_01_isbnregistry USING btree (aggregate_id, id DESC);


--
-- Name: ix_01_snapshot_isbnregistry_event_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_isbnregistry_event_id ON public.snapshots_01_isbnregistry USING btree (event_id);


--
-- Name: ix_01_snapshot_isbnregistry_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_isbnregistry_id ON public.snapshots_01_isbnregistry USING btree (aggregate_id);


--
-- Name: ix_01_snapshot_loan_aggregate_id_and_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_loan_aggregate_id_and_id ON public.snapshots_01_loan USING btree (aggregate_id, id DESC);


--
-- Name: ix_01_snapshot_loan_event_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_loan_event_id ON public.snapshots_01_loan USING btree (event_id);


--
-- Name: ix_01_snapshot_loan_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_loan_id ON public.snapshots_01_loan USING btree (aggregate_id);


--
-- Name: ix_01_snapshot_mailqueue_aggregate_id_and_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_mailqueue_aggregate_id_and_id ON public.snapshots_01_mailqueue USING btree (aggregate_id, id DESC);


--
-- Name: ix_01_snapshot_mailqueue_event_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_mailqueue_event_id ON public.snapshots_01_mailqueue USING btree (event_id);


--
-- Name: ix_01_snapshot_mailqueue_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_mailqueue_id ON public.snapshots_01_mailqueue USING btree (aggregate_id);


--
-- Name: ix_01_snapshot_reservation_aggregate_id_and_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_reservation_aggregate_id_and_id ON public.snapshots_01_reservation USING btree (aggregate_id, id DESC);


--
-- Name: ix_01_snapshot_reservation_event_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_reservation_event_id ON public.snapshots_01_reservation USING btree (event_id);


--
-- Name: ix_01_snapshot_reservation_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_reservation_id ON public.snapshots_01_reservation USING btree (aggregate_id);


--
-- Name: ix_01_snapshot_review_aggregate_id_and_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_review_aggregate_id_and_id ON public.snapshots_01_review USING btree (aggregate_id, id DESC);


--
-- Name: ix_01_snapshot_review_event_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_review_event_id ON public.snapshots_01_review USING btree (event_id);


--
-- Name: ix_01_snapshot_review_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_review_id ON public.snapshots_01_review USING btree (aggregate_id);


--
-- Name: ix_01_snapshot_user_aggregate_id_and_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_user_aggregate_id_and_id ON public.snapshots_01_user USING btree (aggregate_id, id DESC);


--
-- Name: ix_01_snapshot_user_event_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_user_event_id ON public.snapshots_01_user USING btree (event_id);


--
-- Name: ix_01_snapshot_user_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshot_user_id ON public.snapshots_01_user USING btree (aggregate_id);


--
-- Name: ix_01_snapshots_author_timestamp; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshots_author_timestamp ON public.snapshots_01_author USING btree ("timestamp");


--
-- Name: ix_01_snapshots_book_timestamp; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshots_book_timestamp ON public.snapshots_01_book USING btree ("timestamp");


--
-- Name: ix_01_snapshots_editor_timestamp; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshots_editor_timestamp ON public.snapshots_01_editor USING btree ("timestamp");


--
-- Name: ix_01_snapshots_isbnregistry_timestamp; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshots_isbnregistry_timestamp ON public.snapshots_01_isbnregistry USING btree ("timestamp");


--
-- Name: ix_01_snapshots_loan_timestamp; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshots_loan_timestamp ON public.snapshots_01_loan USING btree ("timestamp");


--
-- Name: ix_01_snapshots_mailqueue_timestamp; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshots_mailqueue_timestamp ON public.snapshots_01_mailqueue USING btree ("timestamp");


--
-- Name: ix_01_snapshots_reservation_timestamp; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshots_reservation_timestamp ON public.snapshots_01_reservation USING btree ("timestamp");


--
-- Name: ix_01_snapshots_review_timestamp; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshots_review_timestamp ON public.snapshots_01_review USING btree ("timestamp");


--
-- Name: ix_01_snapshots_user_timestamp; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_01_snapshots_user_timestamp ON public.snapshots_01_user USING btree ("timestamp");


--
-- Name: aggregate_events_01_loan aggregate_events_01_fk; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_loan
    ADD CONSTRAINT aggregate_events_01_fk FOREIGN KEY (event_id) REFERENCES public.events_01_loan(id) MATCH FULL ON DELETE CASCADE;


--
-- Name: aggregate_events_01_editor aggregate_events_01_fk; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_editor
    ADD CONSTRAINT aggregate_events_01_fk FOREIGN KEY (event_id) REFERENCES public.events_01_editor(id) MATCH FULL ON DELETE CASCADE;


--
-- Name: aggregate_events_01_book aggregate_events_01_fk; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_book
    ADD CONSTRAINT aggregate_events_01_fk FOREIGN KEY (event_id) REFERENCES public.events_01_book(id) MATCH FULL ON DELETE CASCADE;


--
-- Name: aggregate_events_01_author aggregate_events_01_fk; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_author
    ADD CONSTRAINT aggregate_events_01_fk FOREIGN KEY (event_id) REFERENCES public.events_01_author(id) MATCH FULL ON DELETE CASCADE;


--
-- Name: aggregate_events_01_reservation aggregate_events_01_fk; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_reservation
    ADD CONSTRAINT aggregate_events_01_fk FOREIGN KEY (event_id) REFERENCES public.events_01_reservation(id) MATCH FULL ON DELETE CASCADE;


--
-- Name: aggregate_events_01_user aggregate_events_01_fk; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_user
    ADD CONSTRAINT aggregate_events_01_fk FOREIGN KEY (event_id) REFERENCES public.events_01_user(id) MATCH FULL ON DELETE CASCADE;


--
-- Name: aggregate_events_01_isbnregistry aggregate_events_01_fk; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_isbnregistry
    ADD CONSTRAINT aggregate_events_01_fk FOREIGN KEY (event_id) REFERENCES public.events_01_isbnregistry(id) MATCH FULL ON DELETE CASCADE;


--
-- Name: aggregate_events_01_mailqueue aggregate_events_01_fk; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_mailqueue
    ADD CONSTRAINT aggregate_events_01_fk FOREIGN KEY (event_id) REFERENCES public.events_01_mailqueue(id) MATCH FULL ON DELETE CASCADE;


--
-- Name: aggregate_events_01_review aggregate_events_01_fk; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aggregate_events_01_review
    ADD CONSTRAINT aggregate_events_01_fk FOREIGN KEY (event_id) REFERENCES public.events_01_review(id) MATCH FULL ON DELETE CASCADE;


--
-- Name: snapshots_01_author event_01_author_fk; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.snapshots_01_author
    ADD CONSTRAINT event_01_author_fk FOREIGN KEY (event_id) REFERENCES public.events_01_author(id) MATCH FULL ON DELETE CASCADE;


--
-- Name: snapshots_01_book event_01_book_fk; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.snapshots_01_book
    ADD CONSTRAINT event_01_book_fk FOREIGN KEY (event_id) REFERENCES public.events_01_book(id) MATCH FULL ON DELETE CASCADE;


--
-- Name: snapshots_01_editor event_01_editor_fk; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.snapshots_01_editor
    ADD CONSTRAINT event_01_editor_fk FOREIGN KEY (event_id) REFERENCES public.events_01_editor(id) MATCH FULL ON DELETE CASCADE;


--
-- Name: snapshots_01_isbnregistry event_01_isbnregistry_fk; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.snapshots_01_isbnregistry
    ADD CONSTRAINT event_01_isbnregistry_fk FOREIGN KEY (event_id) REFERENCES public.events_01_isbnregistry(id) MATCH FULL ON DELETE CASCADE;


--
-- Name: snapshots_01_loan event_01_loan_fk; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.snapshots_01_loan
    ADD CONSTRAINT event_01_loan_fk FOREIGN KEY (event_id) REFERENCES public.events_01_loan(id) MATCH FULL ON DELETE CASCADE;


--
-- Name: snapshots_01_mailqueue event_01_mailqueue_fk; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.snapshots_01_mailqueue
    ADD CONSTRAINT event_01_mailqueue_fk FOREIGN KEY (event_id) REFERENCES public.events_01_mailqueue(id) MATCH FULL ON DELETE CASCADE;


--
-- Name: snapshots_01_reservation event_01_reservation_fk; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.snapshots_01_reservation
    ADD CONSTRAINT event_01_reservation_fk FOREIGN KEY (event_id) REFERENCES public.events_01_reservation(id) MATCH FULL ON DELETE CASCADE;


--
-- Name: snapshots_01_review event_01_review_fk; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.snapshots_01_review
    ADD CONSTRAINT event_01_review_fk FOREIGN KEY (event_id) REFERENCES public.events_01_review(id) MATCH FULL ON DELETE CASCADE;


--
-- Name: snapshots_01_user event_01_user_fk; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.snapshots_01_user
    ADD CONSTRAINT event_01_user_fk FOREIGN KEY (event_id) REFERENCES public.events_01_user(id) MATCH FULL ON DELETE CASCADE;


--
-- PostgreSQL database dump complete
--

\unrestrict yH1FmOtuoQDBCmNhhzK7dVJqhIf5VU5ime7wieIMbrVpcy3HNCJ4PDzYu7tZvMV


--
-- Dbmate schema migrations
--

INSERT INTO public.schema_migrations (version) VALUES
    ('20260315160716'),
    ('20260315160726'),
    ('20260315160730'),
    ('20260315160736'),
    ('20260315160856'),
    ('20260329112908'),
    ('20260405081914'),
    ('20260408164638'),
    ('20260416091649'),
    ('20260435161918');
