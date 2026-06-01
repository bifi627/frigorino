Feature: Settings

  Background:
    Given I am logged in with an active household

  Scenario: Open settings from the user menu
    When I navigate to "/"
    And I open the user menu
    And I click settings in the user menu
    Then the page URL contains "/settings"
    And the language select is visible

  Scenario: Language change persists across reload
    When I navigate to "/settings"
    And I select the language "en"
    And I select the language "de"
    And I reload the page
    Then the persisted language is "de"

  Scenario: Household retention is editable by an owner
    When I navigate to "/household/manage"
    And I set the household retention to "14"
    And I reload the page
    Then the household retention input has value "14"

  Scenario: Household retention is read-only for a member
    Given I am logged in as "alice"
    And an existing household "Family" owned by "bob" with me as a "member"
    When I navigate to "/household/manage"
    Then the household retention input is disabled

  Scenario: Enabling the inventory override reveals the lead input
    Given there is an inventory named "Pantry"
    When I open the inventory edit page for "Pantry"
    And I enable the inventory expiry override
    Then the inventory expiry lead input is visible
