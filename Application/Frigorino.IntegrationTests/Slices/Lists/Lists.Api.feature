Feature: Lists API

  Background:
    Given I am logged in with an active household

  Scenario: Creating a list with an empty name returns a validation error
    When I POST a list with an empty name via the API
    Then the API response status is 400
    And the API response has a validation error for "Name"

  Scenario: Non-member cannot read a household's lists
    Given I am logged in as "alice"
    And an existing household "Other" owned by "bob" that I am not a member of
    When I GET the lists of that household via the API
    Then the API response status is 404

  Scenario: Non-creator Member cannot delete a list
    Given I am logged in as "alice"
    And an existing household "Family" owned by "bob" with me as a "member"
    And "bob" has created a list named "BobsList"
    When I DELETE the list "BobsList" via the API
    Then the API response status is 403

  Scenario: Non-creator Admin can delete a list
    Given I am logged in as "alice"
    And an existing household "Family" owned by "bob" with me as a "admin"
    And "bob" has created a list named "BobsList"
    When I DELETE the list "BobsList" via the API
    Then the API response status is 204
