const js = require('@eslint/js')
const globals = require('globals')

module.exports = [
  {
    // Build output from `npm run build`, plus dependencies.
    ignores: ['lib/**', 'node_modules/**']
  },
  js.configs.recommended,
  {
    files: ['**/*.js'],
    languageOptions: {
      ecmaVersion: 2022,
      sourceType: 'commonjs',
      globals: {
        ...globals.node
      }
    },
    rules: {
      'no-var': 'warn',
      'dot-notation': 'warn',
      'prefer-const': 'warn'
    }
  },
  {
    files: ['test/**/*.js'],
    languageOptions: {
      globals: {
        ...globals.mocha
      }
    }
  }
]
