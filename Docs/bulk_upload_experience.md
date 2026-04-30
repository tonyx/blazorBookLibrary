# The Manager's Experience: Streamlining Our Library with AI-Powered Bulk Upload

I have decided to populate our new digital library starting with the "heavy hitters"—the books that define literature. To do this, I've chosen the Guardian's list of the 100 greatest novels of all time. Manual entry is simply not an option today, so I'm going to demonstrate how we can turn a full day's work into a few minutes of seamless coordination.

### Step 1: Gathering the Data
Let's navigate to the Guardian website. The list is well-curated, but it's formatted for reading rather than database ingestion. Instead of tedious copy-pasting, we're going to use AI to bridge the gap.

Now that I've opened Gemini, I'll provide the URL and give it a specific instruction: *"Extract the ISBNs for every book listed on this page. Return only a single column of ISBNs, nothing else."*

The chatbot returns a clean list in seconds. I've already copied that list into a spreadsheet and exported it as a `.txt` file. This approach is simple, fast, and eliminates manual errors.

### Step 2: The Bulk Import
Let's move back to our **Books Manager** and click on the **Import** feature. This is where the platform really shines. I've uploaded my text file, and now we're going to configure the import flags. To ensure our metadata is as rich as possible, we'll toggle these three settings:

1.  **Generate associated authors when missing**: This flag ensures the system fetches and creates author profiles automatically.
2.  **Allow duplicated ISBN entries**: We'll enable this to ensure the import doesn't stall if there are overlaps.
3.  **Generate automatically embedding**: This is the most critical step. It ensures every book is instantly discoverable via semantic search.

I'll hit the **"Start Import"** button now. 

> *[Note: this is the actual time for importing]*

### Step 3: Sanity Check and Semantic Power
Now that the import has finished, let's enter the **Books** section. As you can see, all 100 books appear here, fully populated with metadata.

To verify the power of our semantic search, I'm going to ask Gemini for a few short, one-sentence descriptions for random books from the list. 

I've gathered these descriptions, and now I'll paste them into our search bar to see how the system performs:
*   **Search 1**: *"A story of a man who tilts at windmills and lives in a world of chivalric romance."*
*   **Result**: *Don Quixote* appears in the **first position**.
*   **Search 2**: *"A powerful narrative about the haunting legacy of slavery and a mother's impossible choice."*
*   **Result**: *Beloved* appears in the **first position**.

### The Verdict
With just a few clicks, we've transformed a raw web list into a fully searchable, metadata-rich library. We've avoided the headache of manual entry, and the semantic search is already working perfectly. Our library is now truly discoverable and ready for our users.



