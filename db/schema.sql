SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: ingestionservicebusingesterdotnet; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA ingestionservicebusingesterdotnet;


--
-- Name: public; Type: SCHEMA; Schema: -; Owner: -
--

-- *not* creating schema, since initdb creates it


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: messages; Type: TABLE; Schema: ingestionservicebusingesterdotnet; Owner: -
--

CREATE TABLE ingestionservicebusingesterdotnet.messages (
    id bigint NOT NULL,
    message_id text NOT NULL,
    event_type text NOT NULL,
    source text NOT NULL,
    body jsonb NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: messages_id_seq; Type: SEQUENCE; Schema: ingestionservicebusingesterdotnet; Owner: -
--

ALTER TABLE ingestionservicebusingesterdotnet.messages ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME ingestionservicebusingesterdotnet.messages_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: schema_migrations; Type: TABLE; Schema: ingestionservicebusingesterdotnet; Owner: -
--

CREATE TABLE ingestionservicebusingesterdotnet.schema_migrations (
    version character varying NOT NULL
);


--
-- Name: messages messages_pkey; Type: CONSTRAINT; Schema: ingestionservicebusingesterdotnet; Owner: -
--

ALTER TABLE ONLY ingestionservicebusingesterdotnet.messages
    ADD CONSTRAINT messages_pkey PRIMARY KEY (id);


--
-- Name: schema_migrations schema_migrations_pkey; Type: CONSTRAINT; Schema: ingestionservicebusingesterdotnet; Owner: -
--

ALTER TABLE ONLY ingestionservicebusingesterdotnet.schema_migrations
    ADD CONSTRAINT schema_migrations_pkey PRIMARY KEY (version);


--
-- Name: idx_messages_created_at; Type: INDEX; Schema: ingestionservicebusingesterdotnet; Owner: -
--

CREATE INDEX idx_messages_created_at ON ingestionservicebusingesterdotnet.messages USING btree (created_at);


--
-- Name: idx_messages_event_type; Type: INDEX; Schema: ingestionservicebusingesterdotnet; Owner: -
--

CREATE INDEX idx_messages_event_type ON ingestionservicebusingesterdotnet.messages USING btree (event_type);


--
-- PostgreSQL database dump complete
--


--
-- Dbmate schema migrations
--

INSERT INTO ingestionservicebusingesterdotnet.schema_migrations (version) VALUES
    ('001');
