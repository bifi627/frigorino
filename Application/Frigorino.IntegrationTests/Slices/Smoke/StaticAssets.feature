Feature: Static asset serving

  Regression coverage for the pre-compressed (.br/.gz) sibling middleware and
  cache headers on the SPA shell. The Vite build emits compressed siblings; the
  PreCompressedStaticFilesMiddleware rewrites Request.Path to serve them; the
  shared StaticFileOptions sets Cache-Control via OnPrepareResponse.

  Scenario: Hashed asset is served Brotli-compressed with immutable cache headers
    When I request a hashed SPA asset with Accept-Encoding "br, gzip"
    Then the static-asset response status is 200
    And the static-asset response header "Content-Encoding" equals "br"
    And the static-asset response header "Vary" contains "Accept-Encoding"
    And the static-asset response header "Cache-Control" equals "public, max-age=31536000, immutable"
    And the static-asset response header "Content-Type" contains "javascript"

  Scenario: Hashed asset without Accept-Encoding is served uncompressed but still immutable
    When I request a hashed SPA asset with Accept-Encoding "identity"
    Then the static-asset response status is 200
    And the static-asset response has no "Content-Encoding" header
    And the static-asset response header "Cache-Control" equals "public, max-age=31536000, immutable"

  Scenario: SPA entry point is served with no-cache, must-revalidate
    When I request "/"
    Then the static-asset response status is 200
    And the static-asset response header "Cache-Control" equals "no-cache, must-revalidate"
    And the static-asset response header "Content-Type" contains "text/html"
