# How GateProof decides

GateProof tells a lender whether a customer really lives at the address they gave, and shows the evidence. This page explains what we check and why, in plain terms, so lenders, auditors and customers can understand a verdict.

The exact thresholds (distances, time windows, counts) are deliberately not published. If they were, it would be easy to tune a fake check-in to pass them. Auditors and regulators who need them can get them under a confidentiality agreement.

## The three verdicts

| Verdict | Meaning |
|---|---|
| **Verified** | Everything we need points to the customer living there, and nothing points the other way. |
| **Needs review** | Something is unclear or slightly off. A person at the lender should look at the evidence before deciding. |
| **Not verified** | We found a strong sign that the customer doesn't live there, or that the evidence was faked. |

Every verdict comes with a confidence level (high, medium or low) and a list of reasons. Each reason has a code (for example `CHECKINS_SEPARATED`) and a plain-English description. **The lender makes the final decision.** A verdict is evidence, not a guarantee.

## What we collect

1. **Consent.** The customer sees who is asking and what will be shared, and agrees. The exact wording they saw is recorded by version.
2. **The home pin.** While standing at home, the customer shares their phone's location once. This is the first check-in.
3. **A gate photo.** Taken with the phone camera at the same moment.
4. **A second check-in, later.** The same link opens again some hours later. The customer shares their location again from home.
5. **Optionally, a utility bill** (electricity or water) showing the address.
6. **NIPOST's building record.** If the lender has given us their own NIPOST key, we look up the registered building at the pin in Nigeria's national postcode system.

Location is read only at the moments the customer taps the button. **Nothing tracks the customer in the background.**

## What we check

**Being at home.** Each check-in is compared with the home pin. Check-ins at the pin support the verdict. A check-in far from the pin fails it. A GPS fix too vague to tell is a red flag.

**Living there, not visiting.** One visit proves little. Living somewhere means being there in the evening and again in the morning, so we look for check-ins at both times of day, hours apart. Two check-ins in one afternoon are inconclusive.

**The same person.** The check-ins should come from the same phone. A different phone for the second check-in sends the case to review. So does one phone that has recently done check-ins for several different customers, which suggests an agent or a fraud ring doing them.

**Faked locations.** Real GPS drifts slightly between readings. Location-faking apps often return exactly the same position every time, so identical coordinates hours apart are a red flag. A web page cannot detect every fake-location app; this is one of several signals rather than a guarantee.

**The photo.** Many phones record when and where a photo was taken. A photo taken just before upload supports the verdict; one taken long before (for example picked from the gallery) is a red flag. If the photo's own location is far from the pin, that is a red flag too. The exact same photo file used for another customer fails the verification. Many phone browsers remove a photo's time and place on upload; then the photo counts neither way, but it is still there for a person to look at.

**NIPOST's register.** A residential or mixed-use building at the pin supports the verdict. A commercial building sends it to review. If NIPOST has no building there (common for new buildings), can't be reached, or the lender has no NIPOST key, that is noted and never held against the customer.

**What a postcode does not prove.** A postcode identifies a building, not who lives in it. NIPOST's data is one signal among several, called with the lender's own key, and a GateProof verdict is never a "NIPOST verification".

**Utility bills.** Bills are kept as evidence for the lender to read. They are not read automatically yet, so they do not change the verdict.

## Privacy

- We collect only what the checks need, and only with consent.
- Device identity never leaves GateProof. The evidence pack says only whether each check-in came from the same phone as the first.
- Photos and bills are deleted after a retention period. Their fingerprints (SHA-256 hashes) stay in the evidence pack, so a copy kept by the lender can still be checked.
- NIPOST data is cleared after the period agreed with NIPOST.

## Known limits

- GPS is less reliable indoors and in dense blocks of flats; one building can hold many households.
- Determined fraud with a rooted phone and a fake-location app can sometimes pass individual checks. Combining several independent signals is what makes it hard.
- The duplicate-photo check catches the exact same file, not a re-saved or cropped copy.
- A verified result shows the customer was at the address at the times checked. It doesn't prove ownership, tenancy or legal residence.
