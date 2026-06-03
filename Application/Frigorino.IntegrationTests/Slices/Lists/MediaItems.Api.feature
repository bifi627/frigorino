Feature: Media Items API

  Background:
    Given I am logged in with an active household

  Scenario: Uploading a photo stores it and serves both renditions
    Given there is a list named "Trip"
    When I upload a photo with caption "beach" to "Trip" via the API
    Then the API response status is 201
    And the uploaded item in "Trip" serves a thumbnail with content-type "image/webp"
    And the uploaded item in "Trip" serves a file with content-type "image/webp"
