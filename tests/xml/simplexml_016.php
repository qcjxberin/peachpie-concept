<?php 

function traverse_xml($xml, $pad = '') {
  $name = $xml->getName();
  echo "$pad<$name";
  foreach($xml->attributes() as $attr => $value)
  {
    echo " $attr=\"$value\"";
  }
  echo ">" . trim($xml) . "\n";
  foreach($xml->children() as $node)
  {
    traverse_xml($node, $pad.'  ');
  }
  echo $pad."</$name>\n";
}

function test() {
  $xml =<<<EOF
<people></people>
EOF;

  $people = simplexml_load_string($xml);
  traverse_xml($people);
  $people->person['name'] = 'John';
  traverse_xml($people);
}

test();
