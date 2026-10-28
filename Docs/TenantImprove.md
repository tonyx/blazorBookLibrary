flowchart TD
    Search[Multi-Tenant Search Results] --> Click[User clicks Book from Library B]
    Click --> Check{Strategy Selection}
    
    subgraph Option A [Option A: Search-Scoped Context]
        Check -->|A| Scope[Pass Considered Tenants Set in UserContext]
        Scope --> ValidateA[Validate book.TenantId in ConsideredTenants]
    end

    subgraph Option B [Option B: Resource-Centric Read Model - Recommended]
        Check -->|B| Resource[Load Book by ID & verify isTenantAccessible]
        Resource --> Details[BookDetails carries TenantReference]
        Details --> UI[BookView renders owning Library info]
    end

    subgraph Option C [Option C: Context Switch on Navigation]
        Check -->|C| Switch[Auto-switch Active Tenant to Library B]
        Switch --> LoadC[Load normally under Library B]
    end
