# Roadmap: fee tracking and maintenance requests

Neither feature is built. Both depend on how a particular hostel works, and guessing wrong would mean building the
wrong thing (for fees: wrong *money*). This note proposes a shape for each and lists the questions that decide it, so
the work can start as soon as they are answered.

Both would follow the pattern of the existing features: tables added to `Database/schema.sql` **and** to
`Database/upgrade-existing-db.sql`, a repository behind an interface (reads throw, writes return an `OperationResult`),
pages guarded by `AccessGuard`, and tests against a real SQL Server in `tests/dotnet/IntegrationTests`, including what
happens when two administrators act at once.

---

## 1. Fee / rent tracking

### What it would do
Administrators record what each resident owes and what they have paid; residents see their own statement.
Payments are **recorded by an administrator** (cash, bank transfer, ...). Taking payments online is a separate and much
larger project (payment provider, PCI scope) and is not proposed here.

### Proposed data

| Table | Purpose | Main columns |
|---|---|---|
| `tbl_Charge` | One amount owed | `UserId`, `RequestId` (the allocation it belongs to), `Period` (month), `Description`, `Amount`, `DueOn`, `CreatedOn`, `CreatedBy` |
| `tbl_Payment` | One amount received | `UserId`, `Amount`, `PaidOn`, `Method`, `Reference`, `Note`, `RecordedBy`, `RecordedOn` |
| `tbl_PaymentAllocation` *(only if payments must be matched to specific charges)* | Which payment settled which charge | `PaymentId`, `ChargeId`, `Amount` |

* A charge copies the room's rent **at the time it is created**, so changing a room's rent later does not rewrite history.
* Money is never edited or deleted. A mistake is fixed with a reversing entry (a negative charge or payment) so the
  record shows what happened and who did it.
* The balance of a resident is `sum(charges) - sum(payments)`; no stored balance that could drift.
* Amounts: `Room.Rent` is a whole number today. If cents/pence matter this becomes `DECIMAL(12,2)` everywhere.

### Screens
* Admin: *Fees* list (every resident's balance, overdue first), *Generate charges for a month* (one charge per current
  allocation), *Record a payment*, a printable receipt.
* Resident: *My fees* (statement of charges, payments and balance).

### Questions only the hostel can answer
1. **Billing period** — monthly, per term/semester, or a fixed stay fee? Is it due in advance or in arrears, and on what day?
2. **Move-in / move-out** — pro-rated for part of a month, or whole months? What is the cut-off day?
3. **Deposit** — is there one, is it refundable, and is it tracked here or elsewhere?
4. **Extras** — electricity, laundry, damage, fines, late fees? Are late fees automatic, and at what rate?
5. **Discounts and waivers** — who may grant them, and must a reason be recorded?
6. **Partial and advance payments** — allowed? Do they settle the oldest charge first (then no `PaymentAllocation` table is needed)?
7. **Receipts** — any legal or tax wording/numbering required? Should a receipt be emailed (the SMTP settings already exist)?
8. **Who may record or reverse a payment** — every administrator, or a separate role? (A separate role means a new account type, see below.)
9. **Overdue** — reminders by email? Any consequence in the system (for example, blocking a new request)?
10. **Currency and rounding** — one currency? Whole units or two decimals?
11. **Existing balances** — is there data (spreadsheet) to import at the start?

---

## 2. Maintenance requests

### What it would do
A resident reports a problem (broken tap, no light) and can follow its progress; administrators triage, assign and close it.

### Proposed data

| Table | Purpose | Main columns |
|---|---|---|
| `tbl_MaintenanceRequest` | One reported problem | `RequestedBy`, `RoomId` (null for common areas), `Category`, `Title`, `Description`, `Priority`, `Status`, `AssignedTo`, `CreatedOn`, `UpdatedOn`, `ResolvedOn`, `ResolutionNote` |
| `tbl_MaintenanceComment` | Conversation on a request | `RequestId`, `AuthorId`, `Body`, `CreatedOn` |

Proposed life cycle (conditional updates, like room requests, so two administrators cannot clash):
`Open` -> `InProgress` -> `Resolved` (-> `Open` again if the resident says it is not fixed); a resident may `Cancel` while `Open`.

### Screens
* Resident: *Report a problem* (their allocated room is pre-selected), *My reports* with status and comments.
* Administrator: queue filterable by status, category and priority; assign; comment; resolve.

### Questions only the hostel can answer
1. **Who can report** — only residents who currently hold a room, or anyone? May a resident report a common-area problem?
2. **Categories and priorities** — what are the lists (plumbing, electrical, ...)? Does the resident choose the priority, or only an administrator?
3. **Who does the work** — do maintenance staff need their own sign-in (a new account type with a restricted view), or do administrators handle everything and just note who was sent?
4. **Notifications** — email the resident when the status changes? Email staff when something urgent is raised?
5. **Photos** — wanted? They add file-upload handling (size and type limits, storage, scanning), which is a security-sensitive addition and should be decided deliberately.
6. **Visibility** — may residents see other residents' reports about shared spaces, or only their own?
7. **Targets** — is there a response-time promise (for example, urgent within 24 hours) that the screen should flag when missed?
8. **Reopening** — for how long after `Resolved` may a resident say "still broken"?

---

## Shared prerequisite: more account types

Today `tbl_User.UserType` allows only `Admin` and `Student` (a `CHECK` constraint), and each type has its own master page
and access rule. A *Staff* or *Accountant* role (answers to fees Q8 and maintenance Q3) means: widening that constraint in
both database scripts, a new master page and an `AccessGuard` rule, and tests for who may reach what. It is worth deciding
before either feature starts, because it changes both.
