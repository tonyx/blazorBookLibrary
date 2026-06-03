alter TABLE 
    item_embeddings_projections
    add column if not exists tenant_id uuid default '5a982f45-1c3a-4f7d-9a54-794ed7696f23'::uuid;

create index if not exists idx_item_embedding_projections_tenant_id on item_embeddings_projections(tenant_id);

alter TABLE
    item_embeddings_projections
    add column if not exists created_at timestamp without time zone;

alter TABLE
    item_embeddings_projections
    add constraint unique_book_id UNIQUE (book_id);