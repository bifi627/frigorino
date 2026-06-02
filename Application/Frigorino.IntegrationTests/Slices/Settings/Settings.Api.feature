Feature: Settings API

  Background:
    Given I am logged in with an active household

  # ---- User settings ----

  Scenario: User settings default to null language
    When I GET my user settings via the API
    Then the API response status is 200
    And the API response has no language

  Scenario: Updating user language lazily creates and persists the setting
    When I PUT my user settings language "de" via the API
    Then the API response status is 200
    When I GET my user settings via the API
    Then the API response status is 200
    And the API response language is "de"

  Scenario: Updating user language with an invalid value returns a validation error
    When I PUT my user settings language "fr" via the API
    Then the API response status is 400
    And the API response has a validation error for "Language"

  # ---- Household settings ----

  Scenario: Household settings default retention is 30
    When I GET the household settings via the API
    Then the API response status is 200
    And the API response retention is 30

  Scenario: Member cannot update household settings
    Given I am logged in as "alice"
    And an existing household "Family" owned by "bob" with me as a "member"
    When I PUT the household settings retention 7 via the API
    Then the API response status is 403

  Scenario: Admin can update household retention and it persists
    Given I am logged in as "alice"
    And an existing household "Family" owned by "bob" with me as a "admin"
    When I PUT the household settings retention 7 via the API
    Then the API response status is 200
    When I GET the household settings via the API
    Then the API response status is 200
    And the API response retention is 7

  Scenario: Non-member cannot read household settings
    Given I am logged in as "alice"
    And an existing household "Other" owned by "bob" that I am not a member of
    When I GET the household settings via the API
    Then the API response status is 404

  Scenario: Non-member cannot update household settings
    Given I am logged in as "alice"
    And an existing household "Other" owned by "bob" that I am not a member of
    When I PUT the household settings retention 7 via the API
    Then the API response status is 404

  Scenario: Updating household retention out of bounds returns a validation error
    Given I am logged in as "alice"
    And an existing household "Family" owned by "bob" with me as a "admin"
    When I PUT the household settings retention 0 via the API
    Then the API response status is 400
    And the API response has a validation error for "CheckedItemRetentionDays"

  # ---- Inventory settings (household-wide placeholder, empty body) ----

  Scenario: Inventory settings endpoint returns 200 for a member
    Given "owner" has created an inventory named "Fridge"
    When I GET the settings of inventory "Fridge" via the API
    Then the API response status is 200

  Scenario: Non-member cannot read inventory settings
    Given I am logged in as "alice"
    And an existing household "Other" owned by "bob" that I am not a member of
    And "bob" has created an inventory named "BobsInventory"
    When I GET the settings of inventory "BobsInventory" via the API
    Then the API response status is 404

  # ---- Per-user inventory notification preferences ----

  Scenario: Personal notification preference defaults to enabled with no lead override
    Given "owner" has created an inventory named "Fridge"
    When I GET my notification preference for inventory "Fridge"
    Then the API response status is 200
    And the notification preference is enabled
    And the API response has no lead

  Scenario: User can set a personal lead-days override and it persists
    Given "owner" has created an inventory named "Fridge"
    When I PUT my notification preference for inventory "Fridge" with enabled true and lead 5
    Then the API response status is 200
    When I GET my notification preference for inventory "Fridge"
    Then the API response status is 200
    And the API response lead is 5

  Scenario: User can mute a specific inventory
    Given "owner" has created an inventory named "Fridge"
    When I PUT my notification preference for inventory "Fridge" with enabled false and no lead
    Then the API response status is 200
    And the notification preference is disabled

  Scenario: Clearing personal lead override to null is valid
    Given "owner" has created an inventory named "Fridge"
    When I PUT my notification preference for inventory "Fridge" with enabled true and no lead
    Then the API response status is 200
    And the API response has no lead

  Scenario: Lead out of bounds returns a validation error
    Given "owner" has created an inventory named "Fridge"
    When I PUT my notification preference for inventory "Fridge" with enabled true and lead 400
    Then the API response status is 400
    And the API response has a validation error for "LeadDays"
