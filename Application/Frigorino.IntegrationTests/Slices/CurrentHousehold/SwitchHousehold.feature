Feature: Switch active household

  Scenario: User creates two households and switches between them
    Given I am logged in as "owner"
    When I navigate to "/household/create"
    And I fill in the household name "Alpha"
    And I submit the household form
    Then I am redirected to "/"
    When I navigate to "/household/create"
    And I fill in the household name "Bravo"
    And I submit the household form
    Then I am redirected to "/"
    When I switch the active household to "Alpha"
    Then the active household should be "Alpha"
    When I switch the active household to "Bravo"
    Then the active household should be "Bravo"
    When I reload the page
    Then the active household should be "Bravo"
