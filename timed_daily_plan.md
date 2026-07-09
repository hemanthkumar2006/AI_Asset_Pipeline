# Daily Timed Plan — Project + DSA
**Full days free · Start time 10:00 AM · Updated for Graphs-first DSA order**

Daily template used every build day below:
- 10:00–10:10 Warm-up/review
- 10:10–11:30 DSA deep work
- 11:30–11:45 Break
- 11:45–1:00 Project block 1
- 1:00–2:00 Lunch
- 2:00–3:30 Project block 2
- 3:30–3:45 Break
- 3:45–4:30 DSA practice/review
- 4:30+ Free

---

## WEEK 1 (Jul 9 – Jul 15) — Setup + Graphs

| Date | Project (11:45–3:30) | DSA (10:10–11:30 & 3:45–4:30) |
|---|---|---|
| Thu Jul 9 | Repo + Unity skeleton; video import + frame extraction script | BFS/DFS fundamentals, implement both; 2 easy problems (Number of Islands, Flood Fill) |
| Fri Jul 10 | Frame preview UI — scrub video, display frame as texture | Graph problems: connected components — 2 problems |
| Sat Jul 11 | Get Replicate/SAM2 API keys, test one manual API call | Graph problems: shortest path (BFS on unweighted graph) — 2 problems |
| Sun Jul 12 | Buffer/rest | Light review of the week's graph problems |
| Mon Jul 13 | Click-to-select: capture click coords, map to pixel coords | Graph problems: cycle detection — 1–2 problems |
| Tue Jul 14 | Send frame + coords to SAM2, get single-frame mask back | Timed practice: 2 graph problems in 45 min |
| Wed Jul 15 | Overlay returned mask on frame in UI | Review weak spots from the week |

## WEEK 2 (Jul 16 – Jul 22) — SAM2 tracking + Trees

| Date | Project | DSA |
|---|---|---|
| Thu Jul 16 | Error/retry handling for the single API call | Tree traversals: preorder/inorder/postorder — implement + 2 problems |
| Fri Jul 17 | Extract N frames around selected frame for tracking test | Tree BFS (level order traversal) — 2 problems |
| Sat Jul 18 | Test SAM2 "propagate mask" across short frame sequence | Binary search tree ops (insert/search) — 2 problems |
| Sun Jul 19 | Buffer | Review |
| Mon Jul 20 | Full SAM2 video tracking across all frames | Tree height/diameter problems — 2 problems |
| Tue Jul 21 | Confidence/drift check for tracking quality | Timed practice: 2 tree problems, 45 min |
| Wed Jul 22 | Re-prompt flow for failed tracking | Review |

## WEEK 3 (Jul 23 – Jul 29) — Segmentation cleanup + Arrays/Strings

| Date | Project | DSA |
|---|---|---|
| Thu Jul 23 | Store per-frame masks + metadata | Arrays: two sum, max subarray — 2 problems |
| Fri Jul 24 | Clean up segmentation module, test on 2 sample videos | Strings: substring/anagram patterns — 2 problems |
| Sat Jul 25 | Bug fixing from tests | Array/string mixed — 2 problems |
| Sun Jul 26 | Buffer | Review |
| Mon Jul 27 | Frame-selection logic: pick 2–4 well-spaced angles | Sorting-based problems — 2 problems |
| Tue Jul 28 | Crop/isolate object in each selected frame using mask | Timed practice: 2 problems, 45 min |
| Wed Jul 29 | Review + polish segmentation pipeline end to end | Review |

## WEEK 4 (Jul 30 – Aug 5) — Multi-view API + Two Pointers/Sliding Window

| Date | Project | DSA |
|---|---|---|
| Thu Jul 30 | Research Meshy/Tripo multi-image API docs, test via curl/Postman | Two pointers — 2 problems |
| Fri Jul 31 | Integrate multi-view API call from Unity (send images, get job ID) | Sliding window — 2 problems |
| Sat Aug 1 | Poll job status, handle async response | Two pointers/sliding window mixed — 2 problems |
| Sun Aug 2 | Buffer | Review |
| Mon Aug 3 | Download resulting mesh (GLB), inspect output | Timed practice: 2 problems, 45 min |
| Tue Aug 4 | Debug/tune multi-view request params | Review weak spots |
| Wed Aug 5 | **Checkpoint: assess progress, cut scope if behind** | Review whole week |

## WEEK 5 (Aug 6 – Aug 12) — Mesh import + Hashmaps

| Date | Project | DSA |
|---|---|---|
| Thu Aug 6 | Import GLB into Unity scene at runtime | Hashmap basics — 2 problems |
| Fri Aug 7 | Basic mesh cleanup — stray verts, scale/orientation | Hashmap + frequency counting — 2 problems |
| Sat Aug 8 | Mesh face adjacency using BFS/DFS (reuse Week 1 graph skills) | Hashmap-based graph problems — 2 problems |
| Sun Aug 9 | Buffer | Review |
| Mon Aug 10 | Connected-components on segmentation masks (same BFS/DFS pattern) | Timed practice: 2 problems, 45 min |
| Tue Aug 11 | Place final mesh + material into scene automatically | Review |
| Wed Aug 12 | End-to-end test: click → mesh in scene | Review |

## WEEK 6 (Aug 13 – Aug 19) — Decimation/LOD + Heaps/Greedy

| Date | Project | DSA |
|---|---|---|
| Thu Aug 13 | Basic decimation step (poly reduction) | Heap basics — 2 problems |
| Fri Aug 14 | Optional LOD tier generation | Greedy problems — 2 problems |
| Sat Aug 15 | Progress UI (stage labels) | Heap/greedy mixed — 2 problems |
| Sun Aug 16 | Buffer | Review |
| Mon Aug 17 | Retry-with-backoff + result caching | Timed practice: 2 problems, 45 min |
| Tue Aug 18 | Full pipeline test end-to-end on 1 video | Review |
| Wed Aug 19 | Fix breakages found | Review |

## WEEK 7 (Aug 20 – Aug 26) — Testing + Basic DP

| Date | Project | DSA |
|---|---|---|
| Thu Aug 20 | Test on 4–5 different videos (varied object types) | 1D DP: climbing stairs style — 2 problems |
| Fri Aug 21 | Fix bugs, tune API params for mesh quality | DP: house robber style — 2 problems |
| Sat Aug 22 | Polish materials/texture import | DP mixed — 2 problems |
| Sun Aug 23 | Buffer | Review |
| Mon Aug 24 | UI polish (loading states, error messages) | Timed practice: 2 problems, 45 min |
| Tue Aug 25 | Record raw demo footage | Review |
| Wed Aug 26 | Review whole pipeline for demo-readiness | Review |

## WEEK 8 (Aug 27 – Sep 2) — Polish, README, Demo + Mixed Review

| Date | Project | DSA |
|---|---|---|
| Thu Aug 27 | Edit demo into a clean 30–45 sec video/GIF | Mixed timed set: 3 problems across all topics |
| Fri Aug 28 | Write README: problem statement, architecture diagram, design decisions | Mixed timed set: 3 problems |
| Sat Aug 29 | Finalize GitHub repo, pin demo at top | Full mock OA: 2 problems, 90 min |
| Sun Aug 30 | Buffer | Review weak topics from all 7 weeks |
| Mon Aug 31 | Write resume bullets for the project | Mock interview-style: explain 1 solution out loud, timed |
| Tue Sep 1 | Final end-to-end sanity check | Full mock OA: 2 problems, 90 min |
| Wed Sep 2 | **Project fully resume-ready** ✅ | Review |

---

## From Sep 3 onward — Apply + Maintain
- Daily: 1 DSA problem (light, don't stop cold)
- 2x/week: timed mock OA or mock interview
- Apply within 1–2 weeks of each company's window opening (Google/Amazon/MS from early Sept, Riot Sept, WBD mid-Sept–Oct, Activision Sept–Jan, startups rolling)
- Track applications in a spreadsheet: company, date applied, status, follow-up date

---

## Rules for using this plan
1. If a day's project task slips, use the Sunday buffer — don't skip DSA to catch up, and don't skip project for extra DSA.
2. Week 5 deliberately reuses Week 1's graph skills (mesh adjacency, connected components) — this is intentional spaced repetition, not a coincidence.
3. Checkpoint on Aug 5 — if behind, cut scope (drop LOD generation first), don't cut testing/polish weeks.
4. Message me at the end of any day you fall behind and I'll re-balance the remaining days rather than letting the plan silently drift.
