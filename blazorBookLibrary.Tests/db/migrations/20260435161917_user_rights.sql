-- migrate:up
GRANT ALL ON TABLE public.aggregate_events_01_Loan TO safe;
GRANT ALL ON SEQUENCE public.aggregate_events_01_Loan_id_seq to safe;
GRANT ALL ON TABLE public.events_01_Loan to safe;
GRANT ALL ON TABLE public.snapshots_01_Loan to safe;
GRANT ALL ON SEQUENCE public.snapshots_01_Loan_id_seq to safe;

GRANT ALL ON TABLE public.aggregate_events_01_Editor TO safe;
GRANT ALL ON SEQUENCE public.aggregate_events_01_Editor_id_seq to safe;
GRANT ALL ON TABLE public.events_01_Editor to safe;
GRANT ALL ON TABLE public.snapshots_01_Editor to safe;
GRANT ALL ON SEQUENCE public.snapshots_01_Editor_id_seq to safe;

GRANT ALL ON TABLE public.aggregate_events_01_Book TO safe;
GRANT ALL ON SEQUENCE public.aggregate_events_01_Book_id_seq to safe;
GRANT ALL ON TABLE public.events_01_Book to safe;
GRANT ALL ON TABLE public.snapshots_01_Book to safe;
GRANT ALL ON SEQUENCE public.snapshots_01_Book_id_seq to safe;

GRANT ALL ON TABLE public.aggregate_events_01_Author TO safe;
GRANT ALL ON SEQUENCE public.aggregate_events_01_Author_id_seq to safe;
GRANT ALL ON TABLE public.events_01_Author to safe;
GRANT ALL ON TABLE public.snapshots_01_Author to safe;
GRANT ALL ON SEQUENCE public.snapshots_01_Author_id_seq to safe;

GRANT ALL ON TABLE public.aggregate_events_01_Reservation TO safe;
GRANT ALL ON SEQUENCE public.aggregate_events_01_Reservation_id_seq to safe;
GRANT ALL ON TABLE public.events_01_Reservation to safe;
GRANT ALL ON TABLE public.snapshots_01_Reservation to safe;
GRANT ALL ON SEQUENCE public.snapshots_01_Reservation_id_seq to safe;

GRANT ALL ON TABLE public.aggregate_events_01_IsbnRegistry TO safe;
GRANT ALL ON SEQUENCE public.aggregate_events_01_IsbnRegistry_id_seq to safe;
GRANT ALL ON TABLE public.events_01_IsbnRegistry to safe;
GRANT ALL ON TABLE public.snapshots_01_IsbnRegistry to safe;
GRANT ALL ON SEQUENCE public.snapshots_01_IsbnRegistry_id_seq to safe;

GRANT ALL ON TABLE public.aggregate_events_01_User TO safe;
GRANT ALL ON SEQUENCE public.aggregate_events_01_User_id_seq to safe;
GRANT ALL ON TABLE public.events_01_User to safe;
GRANT ALL ON TABLE public.snapshots_01_User to safe;
GRANT ALL ON SEQUENCE public.snapshots_01_User_id_seq to safe;

GRANT ALL ON TABLE public.aggregate_events_01_MailQueue TO safe;
GRANT ALL ON SEQUENCE public.aggregate_events_01_MailQueue_id_seq to safe;
GRANT ALL ON TABLE public.events_01_MailQueue to safe;
GRANT ALL ON TABLE public.snapshots_01_MailQueue to safe;
GRANT ALL ON SEQUENCE public.snapshots_01_MailQueue_id_seq to safe;
-- migrate:down

