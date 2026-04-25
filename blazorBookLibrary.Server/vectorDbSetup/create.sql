CREATE TABLE item_embeddings_projections (
    id uuid PRIMARY KEY,      
    book_id uuid NOT NULL,
    vector_data vector(1536),     
    model_name text,             
    last_updated_at timestamp   
);

CREATE INDEX ON item_embeddings_projections 
USING hnsw (vector_data vector_cosine_ops);