<?php

$uri = "http://deki.example.org/@api/deki/site/settings";

$parsed_uri = parse_url($uri);

print_r($uri);
print_r($parsed_uri);

?>