# Sample emails

Real job-application emails, anonymised: the applicant's name is `[NAME]`,
phone numbers are replaced and personal tracking links are removed.
Company names and job titles are kept, because finding those is what the
parser is tested on.

Format: `From:` and `Subject:` header lines, a blank line, then the body.
Blank headers mean the sample was provided without them, so the parser must
work from the body alone.

To add a sample: save it here as `<company>-<kind>.txt` and add a case to
`RuleBasedEmailParserTests.RealEmails`.
