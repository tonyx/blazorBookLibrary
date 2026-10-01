-- Migration: Add paper_id and symbolic metadata/links to item_embeddings_projections
-- 1. Drop existing unique constraint on book_id to allow multiple embeddings per book (e.g. book abstract + individual papers)
ALTER TABLE item_embeddings_projections 
    DROP CONSTRAINT IF EXISTS unique_book_id;

-- 2. Add nullable paper_id (corresponds to PaperId in domain)
ALTER TABLE item_embeddings_projections 
    ADD COLUMN IF NOT EXISTS paper_id uuid NULL;

-- 3. Add partial unique constraints:
--    - A book has at most one book-level embedding (paper_id is NULL) per tenant
CREATE UNIQUE INDEX IF NOT EXISTS unique_book_abstract_embedding 
    ON item_embeddings_projections (tenant_id, book_id) 
    WHERE paper_id IS NULL;

--    - A paper has at most one embedding (paper_id is NOT NULL) per tenant
CREATE UNIQUE INDEX IF NOT EXISTS unique_paper_embedding 
    ON item_embeddings_projections (tenant_id, paper_id) 
    WHERE paper_id IS NOT NULL;

-- 4. Lookup indexes for book and paper queries
CREATE INDEX IF NOT EXISTS idx_item_embeddings_projections_book_id 
    ON item_embeddings_projections (tenant_id, book_id);

CREATE INDEX IF NOT EXISTS idx_item_embeddings_projections_paper_id 
    ON item_embeddings_projections (tenant_id, paper_id) 
    WHERE paper_id IS NOT NULL;

-- 5. Symbolic metadata, tags, and hypertext links
ALTER TABLE item_embeddings_projections
    ADD COLUMN IF NOT EXISTS item_type text DEFAULT 'book',
    ADD COLUMN IF NOT EXISTS tags text[] DEFAULT '{}',
    ADD COLUMN IF NOT EXISTS related_embedding_ids uuid[] DEFAULT '{}',
    ADD COLUMN IF NOT EXISTS related_links jsonb DEFAULT '[]'::jsonb;

-- Backfill item_type for existing records (which are all book-level embeddings)
UPDATE item_embeddings_projections
SET item_type = 'book'
WHERE item_type IS NULL OR item_type = '';

-- 6. GIN indexes for fast tag filtering and JSONB traversal
CREATE INDEX IF NOT EXISTS idx_item_embeddings_projections_tags 
    ON item_embeddings_projections USING gin(tags);

CREATE INDEX IF NOT EXISTS idx_item_embeddings_projections_related_links 
    ON item_embeddings_projections USING gin(related_links);
