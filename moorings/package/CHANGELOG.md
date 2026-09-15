# 0.1.1
- Fixed moorings breaking after a world reload (boat showed as moored, rope gone, couldn't cast off). Post and boat are now linked by a shared tag rather than object IDs, which the game reassigns on load. Boats moored before this update need tying again.
- A post whose boat is gone can be interacted with to clear it.

# 0.1.0
- Mooring post piece (Hammer → Misc). Tie the nearest boat; moored boats take no damage and stay by the post.
