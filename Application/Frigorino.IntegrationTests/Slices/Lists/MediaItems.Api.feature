Feature: Media Items API

  Background:
    Given I am logged in with an active household

  Scenario: Uploading a photo stores it and serves both renditions
    Given there is a list named "Trip"
    When I upload a photo with caption "beach" to "Trip" via the API
    Then the API response status is 201
    And the uploaded item in "Trip" serves a thumbnail with content-type "image/webp"
    And the uploaded item in "Trip" serves a file with content-type "image/webp"

  Scenario: Uploading a document stores it and serves the file without a thumbnail
    Given there is a list named "Trip"
    When I upload a document with caption "warranty" to "Trip" via the API
    Then the API response status is 201
    And the uploaded document in "Trip" serves a file with content-type "application/pdf"
    And the uploaded document in "Trip" has no thumbnail
