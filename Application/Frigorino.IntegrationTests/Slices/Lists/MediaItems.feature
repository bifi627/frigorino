Feature: Media Items

  Background:
    Given I am logged in with an active household

  Scenario: User attaches a photo and views it
    Given there is a list named "Trip"
    When I open the list "Trip"
    And I attach a photo with caption "beach"
    Then a photo thumbnail appears in the list
    When I open the photo
    Then the image lightbox is shown
