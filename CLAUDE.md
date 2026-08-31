# MergeLife

Guidance for Claude Code and other AI assistants working in this repository.

## Language and spelling

- Use American English spellings everywhere: identifiers, comments, log/UI
  strings, commit messages, and documentation. Prefer `color`, `gray`,
  `center`, `behavior`, `neighbor`, `traveled`, `favorite`, `canceled`,
  `analyze`, `normalize`, `initialize`, `license`, `defense`, `modeling`,
  `labeled` over their British forms.
- The only exception is an external API or vendored dependency that dictates
  the spelling — mirror those exactly and do not "fix" them. In this
  repository that covers the vendored Bootstrap assets under
  `js/viewer-web/css/` and `js/viewer-web/js/bootstrap.min.js`, which contain
  British forms and must be left byte-for-byte as shipped upstream.
